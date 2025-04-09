'use strict';

module.exports = {
  up: async (queryInterface, Sequelize) => {
    await queryInterface.createTable('computers', {
      id: {
        type: Sequelize.INTEGER,
        autoIncrement: true,
        primaryKey: true
      },
      name: {
        type: Sequelize.STRING(255),
        allowNull: true
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
        allowNull: true
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
      total_disk_space: {
        type: Sequelize.BIGINT
      },
      gpu_info: {
        type: Sequelize.STRING(255)
      },
      errors: {
        type: Sequelize.JSONB,
        defaultValue: '[]'
      },
      room_id: {
        type: Sequelize.INTEGER,
        references: {
          model: 'rooms',
          key: 'id'
        },
        onUpdate: 'CASCADE',
        onDelete: 'SET NULL'
      },
      created_at: {
        type: Sequelize.DATE,
        allowNull: false
      },
      updated_at: {
        type: Sequelize.DATE,
        allowNull: false
      }
    });
  },

  down: async (queryInterface, Sequelize) => {
    await queryInterface.dropTable('computers');
  }
};