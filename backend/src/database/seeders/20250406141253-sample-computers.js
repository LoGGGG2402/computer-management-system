'use strict';
const { v4: uuidv4 } = require('uuid');

/** @type {import('sequelize-cli').Migration} */
module.exports = {
  async up (queryInterface, Sequelize) {
    // First, get the IDs of the rooms we created in the sample-rooms seeder
    const rooms = await queryInterface.sequelize.query(
      'SELECT id FROM rooms ORDER BY id ASC;',
      { type: queryInterface.sequelize.QueryTypes.SELECT }
    );

    if (rooms.length < 3) {
      console.log('Warning: Not enough rooms found in the database. Make sure to run the rooms seeder first.');
      return;
    }

    // Create sample computers for Lab Room 101 (first room)
    const labRoomId = rooms[0].id;
    const labComputers = [];
    
    // Add computers to the lab room grid (4x6)
    for (let row = 0; row < 6; row++) {
      for (let col = 0; col < 4; col++) {
        labComputers.push({
          name: `LAB-PC-${row * 4 + col + 1}`,
          room_id: labRoomId,
          pos_x: col,
          pos_y: row,
          ip_address: `192.168.1.${100 + row * 4 + col}`,
          unique_agent_id: uuidv4(),
          cpu_info: 'Intel Core i5-10400',
          total_ram: 8 * 1024 * 1024 * 1024, // 8GB in bytes
          errors: '[]',
          created_at: new Date(),
          updated_at: new Date()
        });
      }
    }

    // Create sample computers for Conference Room (second room)
    const conferenceRoomId = rooms[1].id;
    const conferenceComputers = [];
    
    // Add computers to the conference room grid (2x3)
    for (let row = 0; row < 3; row++) {
      for (let col = 0; col < 2; col++) {
        conferenceComputers.push({
          name: `CONF-PC-${row * 2 + col + 1}`,
          room_id: conferenceRoomId,
          pos_x: col,
          pos_y: row,
          ip_address: `192.168.2.${100 + row * 2 + col}`,
          unique_agent_id: uuidv4(),
          cpu_info: 'Intel Core i7-10700',
          total_ram: 16 * 1024 * 1024 * 1024, // 16GB in bytes
          errors: '[]',
          created_at: new Date(),
          updated_at: new Date()
        });
      }
    }

    // Create sample computers for Development Office (third room)
    const devOfficeId = rooms[2].id;
    const devOfficeComputers = [];
    
    // Add computers to the development office grid (5x8)
    for (let row = 0; row < 8; row++) {
      for (let col = 0; col < 5; col++) {
        devOfficeComputers.push({
          name: `DEV-PC-${row * 5 + col + 1}`,
          room_id: devOfficeId,
          pos_x: col,
          pos_y: row,
          ip_address: `192.168.3.${100 + row * 5 + col}`,
          unique_agent_id: uuidv4(),
          cpu_info: 'AMD Ryzen 7 5800X',
          total_ram: 32 * 1024 * 1024 * 1024, // 32GB in bytes
          errors: '[]',
          created_at: new Date(),
          updated_at: new Date()
        });
      }
    }

    // Insert all computers
    await queryInterface.bulkInsert('computers', [
      ...labComputers,
      ...conferenceComputers,
      ...devOfficeComputers
    ], {});
  },

  async down (queryInterface, Sequelize) {
    // Delete all sample computers
    await queryInterface.bulkDelete('computers', null, {});
  }
};
