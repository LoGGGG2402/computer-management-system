'use strict';
const { v4: uuidv4 } = require('uuid');
const bcrypt = require('bcrypt');

module.exports = {
  up: async (queryInterface, Sequelize) => {
    const salt = await bcrypt.genSalt(10);
    
    return queryInterface.bulkInsert('computers', [
      {
        name: 'PC-LAB101-01',
        pos_x: 0,
        pos_y: 0,
        ip_address: '192.168.1.101',
        unique_agent_id: uuidv4(),
        agent_token_hash: await bcrypt.hash('token1', salt),
        last_seen: new Date(),
        os_info: 'Windows 10 Pro, 21H2',
        total_ram: 16 * 1024 * 1024 * 1024, // 16GB in bytes
        cpu_info: 'Intel Core i5-10400 @ 2.90GHz',
        errors: JSON.stringify([]),
        room_id: 1,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        name: 'PC-LAB101-02',
        pos_x: 1,
        pos_y: 0,
        ip_address: '192.168.1.102',
        unique_agent_id: uuidv4(),
        agent_token_hash: await bcrypt.hash('token2', salt),
        last_seen: new Date(),
        os_info: 'Windows 10 Pro, 21H2',
        total_ram: 16 * 1024 * 1024 * 1024,
        cpu_info: 'Intel Core i5-10400 @ 2.90GHz',
        errors: JSON.stringify([]),
        room_id: 1,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        name: 'PC-SE-01',
        pos_x: 0,
        pos_y: 0,
        ip_address: '192.168.2.101',
        unique_agent_id: uuidv4(),
        agent_token_hash: await bcrypt.hash('token3', salt),
        last_seen: new Date(),
        os_info: 'Windows 11 Pro, 22H2',
        total_ram: 32 * 1024 * 1024 * 1024, // 32GB in bytes
        cpu_info: 'Intel Core i7-12700K @ 3.60GHz',
        errors: JSON.stringify([]),
        room_id: 2,
        created_at: new Date(),
        updated_at: new Date()
      },
      {
        name: 'PC-NET-01',
        pos_x: 0,
        pos_y: 0,
        ip_address: '192.168.3.101',
        unique_agent_id: uuidv4(),
        agent_token_hash: await bcrypt.hash('token4', salt),
        last_seen: new Date(),
        os_info: 'Windows 10 Enterprise, 21H2',
        total_ram: 32 * 1024 * 1024 * 1024,
        cpu_info: 'AMD Ryzen 7 5800X @ 3.80GHz',
        errors: JSON.stringify([]),
        room_id: 3,
        created_at: new Date(),
        updated_at: new Date()
      }
    ]);
  },

  down: async (queryInterface, Sequelize) => {
    return queryInterface.bulkDelete('computers', null, {});
  }
};