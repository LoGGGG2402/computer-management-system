/**
 * Refresh Token Management Strategy:
 * 1.  **Selector/Verifier Model**:
 * -   When a refresh token is created, it consists of two parts: `selector` and `secretPart`.
 * -   `selector`: A random, unique string stored directly in the database and indexed for fast lookups ($O(\log N)$ or $O(1)$ depending on the DB).
 * -   `secretPart`: Another random string, hashed using bcrypt and stored as `hashed_verifier` in the database.
 * -   The full token sent to the client (and stored in an HttpOnly cookie) is in the format `selector.secretPart`.
 *
 * 2.  **Refresh Token Validation**:
 * -   The client sends the `selector.secretPart` token.
 * -   The service splits the `selector` and `secretPart`.
 * -   The `selector` is used to find the token record in the database.
 * -   If found, the `secretPart` (from the client) is compared against the `hashed_verifier` (from the DB) using `bcrypt.compare()`.
 *
 * 3.  **Token Rotation**:
 * -   Each time a refresh token is successfully used to obtain a new access token, that refresh token is invalidated (deleted from the DB).
 * -   A new pair of access token and refresh token (with a completely new `selector` and `secretPart`) is generated and returned to the client.
 * -   This mitigates the risk if a refresh token is compromised, as it can only be used once.
 *
 * 4.  **Enhanced Security**:
 * -   **Tampering/Theft Detection**: If a valid `selector` is found in the DB, but the accompanying `secretPart` does not match the `hashed_verifier`, this is considered a potential sign of token theft or tampering. In this case, all refresh tokens for the associated user are invalidated (`invalidateAllUserTokens`) to protect the account.
 * -   **Token Revocation**: Provides mechanisms to revoke a specific refresh token (e.g., on logout) or all refresh tokens for a user (e.g., on password change or suspicious activity detection).
 * -   **HttpOnly Cookies**: Refresh tokens should be stored in HttpOnly cookies to prevent access from client-side JavaScript (XSS).
 */
const jwt = require("jsonwebtoken");
const crypto = require("crypto");
const bcrypt = require("bcrypt");
const db = require("../database/models");
const authConfig = require("../config/auth.config");

const User = db.User;
const RefreshToken = db.RefreshToken;

class AuthService {
  /**
   * Authenticates user credentials and issues tokens.
   * @async
   * @param {string} username - The user's username.
   * @param {string} password - The user's password.
   * @returns {Promise<object>} An object containing user information, access token, and refresh token.
   * @property {number} id - User ID.
   * @property {string} username - Username.
   * @property {string} role - User role.
   * @property {boolean} is_active - User's active status.
   * @property {string} token - JWT access token string.
   * @property {Date} expires_at - Access token expiration timestamp.
   * @property {string} refreshToken - Refresh token string (`selector.secretPart` format) to be stored in a cookie.
   * @throws {Error} If the user is not found, inactive, or the password is incorrect.
   */
  async login(username, password) {
    try {
      const user = await User.findOne({ where: { username, is_active: true } });
      if (!user) {
        throw new Error("User not found or inactive");
      }

      const passwordIsValid = await user.validPassword(password);
      if (!passwordIsValid) {
        throw new Error("Invalid password");
      }

      const accessToken = await this.generateAccessToken(user);
      const refreshTokenData = await this.generateRefreshToken(user.id);

      return {
        id: user.id,
        username: user.username,
        role: user.role,
        is_active: user.is_active,
        token: accessToken.token,
        expires_at: accessToken.expires_at,
        refreshToken: refreshTokenData.tokenForCookie,
      };
    } catch (error) {
      throw error;
    }
  }

  /**
   * Retrieves user details by ID.
   * @async
   * @param {number} userId - The ID of the user.
   * @returns {Promise<User|null>} The user object or null if not found.
   * @throws {Error} If the user is not found.
   */
  async getUserById(userId) {
    try {
      const user = await User.findByPk(userId, {
        attributes: [
          "id",
          "username",
          "role",
          "is_active",
          "created_at",
          "updated_at",
        ],
      });
      if (!user) {
        throw new Error("User not found");
      }
      return user;
    } catch (error) {
      throw error;
    }
  }

  /**
   * Generates a JWT access token for the user.
   * @async
   * @param {User} user - The authenticated user object.
   * @returns {Promise<object>} An object containing the access token and its expiration time.
   * @property {string} token - JWT access token string.
   * @property {Date} expires_at - Access token expiration timestamp.
   */
  async generateAccessToken(user) {
    const expiresIn = authConfig.accessToken.expiresIn;
    const expiresInMs = this.parseExpiresIn(expiresIn);
    const expires_at = new Date(Date.now() + expiresInMs);

    const token = jwt.sign(
      {
        id: user.id,
        username: user.username,
        role: user.role,
      },
      authConfig.accessToken.secret,
      { expiresIn }
    );

    return {
      token,
      expires_at,
    };
  }

  /**
   * Generates a new refresh token for the user using the Selector/Verifier model.
   * @async
   * @param {number} userId - The ID of the user.
   * @returns {Promise<object>} An object containing the refresh token (in `selector.secretPart` format for the cookie),
   * selector, expiration time, and DB record ID.
   * @property {string} tokenForCookie - The full refresh token (`selector.secretPart`) to be sent to the client.
   * @property {string} selector - The selector part of the token.
   * @property {Date} expires_at - Refresh token expiration timestamp.
   * @property {number} db_id - The ID of the refresh token record in the database.
   */
  async generateRefreshToken(userId) {
    const selector = crypto.randomBytes(16).toString("hex");
    const secretPart = crypto.randomBytes(32).toString("hex");
    const tokenForCookie = `${selector}.${secretPart}`;

    const salt = await bcrypt.genSalt(10);
    const hashedVerifier = await bcrypt.hash(secretPart, salt);

    const expiresIn = authConfig.refreshToken.expiresIn;
    const expiresInMs = this.parseExpiresIn(expiresIn);
    const expires_at = new Date(Date.now() + expiresInMs);
    const issued_at = new Date();

    const refreshTokenRecord = await RefreshToken.create({
      user_id: userId,
      selector: selector,
      hashed_verifier: hashedVerifier,
      expires_at,
      issued_at,
    });

    return {
      tokenForCookie,
      selector,
      expires_at,
      db_id: refreshTokenRecord.id,
    };
  }

  /**
   * Parses an expiration time string (e.g., '7d', '1h') into milliseconds.
   * @param {string} expiresIn - The expiration time string.
   * @returns {number} The corresponding number of milliseconds, or 0 if the format is invalid.
   */
  parseExpiresIn(expiresIn) {
    if (typeof expiresIn !== "string" || expiresIn.length < 2) return 0;
    const unit = expiresIn.charAt(expiresIn.length - 1).toLowerCase();
    const value = parseInt(expiresIn.slice(0, -1), 10);

    if (isNaN(value)) return 0;

    switch (unit) {
      case "s":
        return value * 1000;
      case "m":
        return value * 60 * 1000;
      case "h":
        return value * 60 * 60 * 1000;
      case "d":
        return value * 24 * 60 * 60 * 1000;
      default:
        return 0;
    }
  }

  /**
   * Refreshes an access token using a refresh token.
   * Implements token rotation: the old refresh token is destroyed, and a new pair of tokens is generated.
   * @async
   * @param {string} tokenFromCookie - The refresh token (`selector.secretPart` format) from the client's cookie.
   * @returns {Promise<object>} An object containing the new access token, new refresh token, and user information.
   * @throws {Error} If the refresh token is invalid, expired, the user is not found/inactive,
   * or if there are signs of tampering (verifier mismatch).
   */
  async refreshAuthToken(tokenFromCookie) {
    if (
      !tokenFromCookie ||
      typeof tokenFromCookie !== "string" ||
      !tokenFromCookie.includes(".")
    ) {
      throw new Error("Invalid refresh token format");
    }

    const [selector, secretPart] = tokenFromCookie.split(".", 2);

    if (!selector || !secretPart) {
      throw new Error("Malformed refresh token");
    }

    const tokenRecord = await RefreshToken.findOne({ where: { selector } });

    if (!tokenRecord) {
      throw new Error("Invalid refresh token (selector not found)");
    }

    const isMatch = await bcrypt.compare(
      secretPart,
      tokenRecord.hashed_verifier
    );

    if (!isMatch) {
      await this.invalidateAllUserTokens(tokenRecord.user_id);
      throw new Error(
        "Invalid refresh token (verifier mismatch - potential tampering, all tokens for user invalidated)"
      );
    }

    if (tokenRecord.expires_at < new Date()) {
      await tokenRecord.destroy();
      throw new Error("Refresh token expired");
    }

    const user = await User.findByPk(tokenRecord.user_id);
    if (!user || !user.is_active) {
      await tokenRecord.destroy();
      throw new Error("User not found or inactive");
    }

    await tokenRecord.destroy();

    const newAccessToken = await this.generateAccessToken(user);
    const newRefreshTokenData = await this.generateRefreshToken(user.id);

    return {
      newAccessToken,
      newRefreshToken: newRefreshTokenData,
      user: {
        id: user.id,
        username: user.username,
        role: user.role,
        is_active: user.is_active,
      },
    };
  }

  /**
   * Revokes a specific refresh token.
   * @async
   * @param {string} tokenFromCookie - The refresh token (`selector.secretPart` format) to be revoked.
   * @returns {Promise<boolean>} `true` if revocation was successful, `false` otherwise.
   * Verifying the `secretPart` before deletion is an additional security measure to ensure
   * only the legitimate owner can request revocation.
   */
  async revokeRefreshToken(tokenFromCookie) {
    if (
      !tokenFromCookie ||
      typeof tokenFromCookie !== "string" ||
      !tokenFromCookie.includes(".")
    ) {
      return false;
    }

    const [selector, secretPart] = tokenFromCookie.split(".", 2);

    if (!selector || !secretPart) {
      return false;
    }

    const tokenRecord = await RefreshToken.findOne({ where: { selector } });

    if (tokenRecord) {
      const isMatch = await bcrypt.compare(
        secretPart,
        tokenRecord.hashed_verifier
      );
      if (isMatch) {
        await tokenRecord.destroy();
        return true;
      } else {
        return false;
      }
    }
    return false;
  }

  /**
   * Logs out the user by revoking their current refresh token.
   * This is an alias for `revokeRefreshToken`.
   * @async
   * @param {string} refreshTokenString - The refresh token (`selector.secretPart` format) to be revoked.
   * @returns {Promise<boolean>} `true` if logout (token revocation) was successful, `false` otherwise.
   */
  async logout(refreshTokenString) {
    return this.revokeRefreshToken(refreshTokenString);
  }

  /**
   * Invalidates (deletes) all refresh tokens for a specific user.
   * Useful in cases like password changes or detection of suspicious activity.
   * @async
   * @param {number} userId - The ID of the user.
   * @returns {Promise<number>} The number of refresh tokens that were deleted.
   */
  async invalidateAllUserTokens(userId) {
    const result = await RefreshToken.destroy({
      where: { user_id: userId },
    });
    return result;
  }
}

module.exports = new AuthService();
