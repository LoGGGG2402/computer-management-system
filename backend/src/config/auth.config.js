module.exports = {
  secret: process.env.JWT_SECRET || 'cms-super-secret-key',
  expiresIn: process.env.JWT_EXPIRES_IN || '24h' // Token expires in 24 hours
};