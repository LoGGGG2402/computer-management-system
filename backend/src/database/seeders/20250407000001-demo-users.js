'use strict';
const bcrypt = require('bcrypt');

module.exports = {
  up: async (queryInterface, Sequelize) => {
    const salt = await bcrypt.genSalt(10);
    const admin_password = await bcrypt.hash('admin123', salt);
    const user_password = await bcrypt.hash('user123', salt);
    
    return queryInterface.bulkInsert('users', [
      {
        username: 'admin',
        password_hash: admin_password,
        role: 'admin',
        is_active: true,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        username: 'user1',
        password_hash: user_password,
        role: 'user',
        is_active: true,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        username: 'user2',
        password_hash: user_password,
        role: 'user',
        is_active: true,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        username: 'user3',
        password_hash: user_password,
        role: 'user',
        is_active: false,
        created_at: new Date(),
        updated_at: new Date()
      }
    ]);
  },

  down: async (queryInterface, Sequelize) => {
    return queryInterface.bulkDelete('users', null, {});
  }
};