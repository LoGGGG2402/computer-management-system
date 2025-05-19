module.exports = {
  accessToken: {
    secret: process.env.ACCESS_TOKEN_SECRET || "cms-access-token-secret",
    expiresIn: process.env.ACCESS_TOKEN_EXPIRES_IN || "1h",
  },
  refreshToken: {
    secret: process.env.REFRESH_TOKEN_SECRET || "cms-refresh-token-secret",
    expiresIn: process.env.REFRESH_TOKEN_EXPIRES_IN || "7d",
    cookieMaxAge: 7 * 24 * 60 * 60 * 1000,
  },
};
