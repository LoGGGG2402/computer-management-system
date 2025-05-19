"use strict";

module.exports = {
  up: async (queryInterface, Sequelize) => {
    return queryInterface.bulkInsert("user_room_assignments", [
      {
        user_id: 1, // admin user
        room_id: 1, // Computer Lab 101
      },
      {
        user_id: 1, // admin user
        room_id: 2, // Software Engineering Lab
      },
      {
        user_id: 1, // admin user
        room_id: 3, // Network Lab
      },
      {
        user_id: 2, // user1
        room_id: 1, // Computer Lab 101
      },
      {
        user_id: 2, // user1
        room_id: 2, // Software Engineering Lab
      },
      {
        user_id: 3, // user2
        room_id: 2, // Software Engineering Lab
      },
      {
        user_id: 3, // user2
        room_id: 3, // Network Lab
      },
    ]);
  },

  down: async (queryInterface, Sequelize) => {
    return queryInterface.bulkDelete("user_room_assignments", null, {});
  },
};
