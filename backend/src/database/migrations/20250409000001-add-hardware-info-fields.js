'use strict';

module.exports = {
  up: async (queryInterface, Sequelize) => {
    await queryInterface.addColumn('computers', 'total_disk_space', {
      type: Sequelize.BIGINT,
      allowNull: true
    });

    await queryInterface.addColumn('computers', 'gpu_info', {
      type: Sequelize.STRING(255),
      allowNull: true
    });
  },

  down: async (queryInterface, Sequelize) => {
    await queryInterface.removeColumn('computers', 'total_disk_space');
    await queryInterface.removeColumn('computers', 'gpu_info');
  }
};