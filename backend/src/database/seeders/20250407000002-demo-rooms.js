"use strict";

module.exports = {
  up: async (queryInterface, Sequelize) => {
    return queryInterface.bulkInsert("rooms", [
      {
        name: "Computer Lab 101",
        description: "Main computer laboratory for 1st year students",
        layout: JSON.stringify({
          columns: 6,
          rows: 5,
        }),
        created_at: new Date(),
        updated_at: new Date(),
      },
      {
        name: "Software Engineering Lab",
        description: "Advanced computing lab for software engineering courses",
        layout: JSON.stringify({
          columns: 5,
          rows: 4,
        }),
        created_at: new Date(),
        updated_at: new Date(),
      },
      {
        name: "Network Lab",
        description: "Specialized lab for networking and cybersecurity courses",
        layout: JSON.stringify({
          columns: 4,
          rows: 4,
        }),
        created_at: new Date(),
        updated_at: new Date(),
      },
    ]);
  },

  down: async (queryInterface, Sequelize) => {
    return queryInterface.bulkDelete("rooms", null, {});
  },
};
