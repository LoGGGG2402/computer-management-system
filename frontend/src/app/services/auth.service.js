import api from './api';

/**
 * In-memory token storage
 */
const tokenStorage = {
  token: null,
  
  setToken(token) {
    this.token = token;
  },
  
  getToken() {
    return this.token;
  },
  
  clearToken() {
    this.token = null;
  }
};

/**
 * User info storage in localStorage
 */
const userStorage = {
  setUserInfo(userData) {
    const userInfo = {
      id: userData.id,
      username: userData.username,
      role: userData.role,
      is_active: userData.is_active,
      expires_at: userData.expires_at
    };
    localStorage.setItem('userInfo', JSON.stringify(userInfo));
  },
  
  getUserInfo() {
    const storedData = localStorage.getItem('userInfo');
    if (!storedData) return null;
    try {
      return JSON.parse(storedData);
    } catch (error) {
      console.error('Error parsing stored user info:', error);
      return null;
    }
  },
  
  clearUserInfo() {
    localStorage.removeItem('userInfo');
  }
};

/**
 * Authentication service
 */
class AuthService {
  get tokenStorage() {
    return tokenStorage;
  }

  /**
   * Refresh the access token
   * @returns {Promise<Object>} Updated user data with new access token
   */
  async refreshToken() {
    try {
      const response = await api.post(`/auth/refresh-token`);
      
      if (response.data.status === 'success' && response.data.data.token) {
        const newToken = response.data.data.token;
        const userInfo = userStorage.getUserInfo();
        
        tokenStorage.setToken(newToken);
        
        return {
          ...userInfo,
          token: newToken,
          expires_at: response.data.data.expires_at
        };
      }
      
      throw new Error('Invalid refresh token response');
    } catch (error) {
      throw error;
    }
  }

  /**
   * Login user
   * @param {string} username - Username
   * @param {string} password - Password
   * @returns {Promise<Object>} User data
   */
  async login(username, password) {
    try {
      const response = await api.post(`/auth/login`, {
        username,
        password
      });
      
      if (response.data.status === 'success' && response.data.data.token) {
        const userData = response.data.data;
        
        tokenStorage.setToken(userData.token);
        userStorage.setUserInfo(userData);
        
        return userData;
      }
      
      return null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Login failed. Please check your credentials.';
      console.error('Login error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Logout user
   */
  async logout() {
    try {
      await api.post(`/auth/logout`);
    } catch (error) {
      console.error('Error during server logout:', error);
    } finally {
      tokenStorage.clearToken();
      userStorage.clearUserInfo();
    }
  }

  /**
   * Handle refresh token error
   */
  async handleRefreshTokenError() {
    try {
      await this.logout();
    } catch (error) {
      console.error('Error during logout after refresh token failure:', error);
    } finally {
      tokenStorage.clearToken();
      userStorage.clearUserInfo();
      
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
  }

  /**
   * Get user profile
   * @returns {Promise<Object>} User profile data
   */
  async getProfile() {
    try {
      const response = await api.get(`/auth/me`);
      return response.data.status === 'success' ? response.data.data : null;
    } catch (error) {
      const errorMessage = error.extractedMessage || 'Failed to fetch user profile';
      console.error('Profile error:', errorMessage);
      throw new Error(errorMessage);
    }
  }

  /**
   * Check if user is authenticated
   * @returns {boolean} Authentication status
   */
  isAuthenticated() {
    return !!this.getCurrentUser()?.token;
  }
  
  /**
   * Get current user data
   * @returns {Object|null} User data
   */
  getCurrentUser() {
    const userInfo = userStorage.getUserInfo();
    if (!userInfo) return null;
    
    return {
      ...userInfo,
      token: tokenStorage.getToken()
    };
  }
}

export default new AuthService();