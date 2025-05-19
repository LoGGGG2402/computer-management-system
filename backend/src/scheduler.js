/**
 * @fileoverview Token cleanup scheduler
 * Automatically removes expired refresh tokens from the database on a scheduled basis
 * Runs daily at midnight to keep the refresh_tokens table clean and prevent it from growing too large
 */
const cron = require('node-cron');
const db = require('./database/models');
const logger = require('./utils/logger');

const RefreshToken = db.RefreshToken;
const { Op } = db.Sequelize;

/**
 * Schedule a task to clean up expired refresh tokens
 * Runs at 00:00 (midnight) every day
 */
function initializeTokenCleanupScheduler() {
  logger.info('Initializing token cleanup scheduler');
  
  // Schedule the cleanup job to run at midnight (00:00) every day
  cron.schedule('0 0 * * *', async () => {
    logger.info('Running scheduled job: Clean up expired refresh tokens');
    try {
      const now = new Date();
      const result = await RefreshToken.destroy({
        where: {
          expires_at: {
            [Op.lt]: now,
          },
        },
      });
      logger.info(`Expired refresh tokens cleanup: ${result} tokens deleted.`);
    } catch (error) {
      logger.error('Error during expired refresh tokens cleanup:', {
        error: error.message,
        stack: error.stack
      });
    }
  });
  
  logger.info('Token cleanup scheduler initialized successfully');
}

module.exports = {
  initializeTokenCleanupScheduler
};
