'use strict';

/** @type {import('sequelize-cli').Migration} */
module.exports = {
  async up (queryInterface, Sequelize) {
    await queryInterface.createTable('computers', {
      id: {
        type: Sequelize.INTEGER,
        autoIncrement: true,
        primaryKey: true
      },
      name: {
        type: Sequelize.STRING(255),
        allowNull: true // NULL initially as per readme
      },
      room_id: {
        type: Sequelize.INTEGER,
        allowNull: true, // NULL initially as per readme
        references: {
          model: 'rooms',
          key: 'id'
        },
        onUpdate: 'CASCADE',
        onDelete: 'SET NULL'
      },
      pos_x: {
        type: Sequelize.INTEGER,
        defaultValue: 0
      },
      pos_y: {
        type: Sequelize.INTEGER,
        defaultValue: 0
      },
      ip_address: {
        type: Sequelize.STRING(50)
      },
      unique_agent_id: {
        type: Sequelize.STRING(255),
        allowNull: false,
        unique: true
      },
      agent_token_hash: {
        type: Sequelize.STRING(255),
        allowNull: true // NULL until successful registration
      },
      last_seen: {
        type: Sequelize.DATE
      },
      windows_version: {
        type: Sequelize.STRING(255)
      },
      total_ram: {
        type: Sequelize.BIGINT
      },
      cpu_info: {
        type: Sequelize.STRING(255)
      },
      errors: {
        type: Sequelize.JSONB,
        defaultValue: '[]'
      },
      created_at: {
        allowNull: false,
        type: Sequelize.DATE,
        defaultValue: Sequelize.literal('CURRENT_TIMESTAMP')
      },
      updated_at: {
        allowNull: false,
        type: Sequelize.DATE,
        defaultValue: Sequelize.literal('CURRENT_TIMESTAMP')
      }
    });
  },

  async down (queryInterface, Sequelize) {
    await queryInterface.dropTable('computers');
  }
};
