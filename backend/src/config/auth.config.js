module.exports = {
  // Access token configuration
  accessToken: {
    secret: process.env.ACCESS_TOKEN_SECRET || 'cms-access-token-secret',
    expiresIn: process.env.ACCESS_TOKEN_EXPIRES_IN || '1h' // Access token expires in 1 hour
  },
  // Refresh token configuration
  refreshToken: {
    secret: process.env.REFRESH_TOKEN_SECRET || 'cms-refresh-token-secret',
    expiresIn: process.env.REFRESH_TOKEN_EXPIRES_IN || '7d', // Refresh token expires in 7 days
    cookieMaxAge: 7 * 24 * 60 * 60 * 1000 // 7 days in milliseconds
  }
};