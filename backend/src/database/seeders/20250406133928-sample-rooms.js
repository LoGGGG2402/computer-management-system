'use strict';

/** @type {import('sequelize-cli').Migration} */
module.exports = {
  async up(queryInterface, Sequelize) {
    await queryInterface.bulkInsert('rooms', [
      {
        name: 'Lab Room 101',
        description: 'Main computer lab on first floor',
        layout: JSON.stringify({
          width: 800,
          height: 600,
          background: '#f5f5f5',
          grid: {
            columns: 4,    // 4 computers across (X-axis)
            rows: 6,       // 6 computers down (Y-axis)
            spacing_x: 150, // Horizontal spacing between computers
            spacing_y: 80  // Vertical spacing between computers
          }
        }),
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        name: 'Conference Room A',
        description: 'Meeting room with presentation equipment',
        layout: JSON.stringify({
          width: 600,
          height: 400,
          background: '#e0e0e0',
          grid: {
            columns: 2,    // 2 computers across (X-axis)
            rows: 3,       // 3 computers down (Y-axis)
            spacing_x: 200, // Horizontal spacing between computers
            spacing_y: 100  // Vertical spacing between computers
          }
        }),
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        name: 'Development Office',
        description: 'Software development team workspace',
        layout: JSON.stringify({
          width: 1000,
          height: 800,
          background: '#f0f0f0',
          grid: {
            columns: 5,    // 5 computers across (X-axis)
            rows: 8,       // 8 computers down (Y-axis)
            spacing_x: 180, // Horizontal spacing between computers
            spacing_y: 90   // Vertical spacing between computers
          }
        }),
        created_at: new Date(),
        updated_at: new Date()
      }
    ], {});
  },

  async down(queryInterface, Sequelize) {
    await queryInterface.bulkDelete('rooms', null, {});
  }
};
