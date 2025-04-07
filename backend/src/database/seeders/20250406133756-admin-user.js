'use strict';
const bcrypt = require('bcrypt');

/** @type {import('sequelize-cli').Migration} */
module.exports = {
  async up(queryInterface, Sequelize) {
    // Check if admin user already exists
    const adminExists = await queryInterface.sequelize.query(
      "SELECT id FROM users WHERE username = 'admin'",
      { type: queryInterface.sequelize.QueryTypes.SELECT }
    );

    // Check if regular user already exists
    const userExists = await queryInterface.sequelize.query(
      "SELECT id FROM users WHERE username = 'user'",
      { type: queryInterface.sequelize.QueryTypes.SELECT }
    );

    // Hash the passwords
    const adminPasswordHash = await bcrypt.hash('Admin@123', 10);
    const userPasswordHash = await bcrypt.hash('User@123', 10);

    // If admin exists, update password; otherwise insert new admin
    if (adminExists.length > 0) {
      await queryInterface.sequelize.query(
        `UPDATE users SET password_hash = :adminPasswordHash WHERE username = 'admin'`,
        {
          replacements: { adminPasswordHash },
          type: queryInterface.sequelize.QueryTypes.UPDATE
        }
      );
      console.log('Admin user password updated');
    } else {
      // Insert admin user
      await queryInterface.bulkInsert('users', [
        {
          username: 'admin',
          password_hash: adminPasswordHash,
          role: 'admin',
          is_active: true,
          created_at: new Date(),
          updated_at: new Date()
        }
      ], {});
      console.log('Admin user created');
    }

    // If regular user exists, update password; otherwise insert new user
    if (userExists.length > 0) {
      await queryInterface.sequelize.query(
        `UPDATE users SET password_hash = :userPasswordHash WHERE username = 'user'`,
        {
          replacements: { userPasswordHash },
          type: queryInterface.sequelize.QueryTypes.UPDATE
        }
      );
      console.log('Regular user password updated');
    } else {
      // Insert regular user
      await queryInterface.bulkInsert('users', [
        {
          username: 'user',
          password_hash: userPasswordHash,
          role: 'user',
          is_active: true,
          created_at: new Date(),
          updated_at: new Date()
        }
      ], {});
      console.log('Regular user created');
    }
  },

  async down(queryInterface, Sequelize) {
    // Remove the users
    await queryInterface.bulkDelete('users', { 
      username: {
        [Sequelize.Op.in]: ['admin', 'user']
      }
    }, {});
  }
};
