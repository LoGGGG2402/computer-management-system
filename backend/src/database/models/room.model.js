module.exports = (sequelize, DataTypes) => {
  const Room = sequelize.define(
    "Room",
    {
      id: {
        type: DataTypes.INTEGER,
        autoIncrement: true,
        primaryKey: true,
      },
      name: {
        type: DataTypes.STRING(255),
        allowNull: false,
      },
      description: {
        type: DataTypes.TEXT,
      },
      layout: {
        type: DataTypes.JSONB,
        defaultValue: {
          columns: 4,
          rows: 4,
        },
      },
    },
    {
      tableName: "rooms",
      timestamps: true,
      underscored: true,
      createdAt: "created_at",
      updatedAt: "updated_at",
    }
  );

  Room.associate = (models) => {
    Room.belongsToMany(models.User, {
      through: "user_room_assignments",
      as: "assignedUsers",
      foreignKey: "room_id",
    });

    // One-to-many relationship with Computer
    Room.hasMany(models.Computer, {
      foreignKey: "room_id",
      as: "computers",
    });
  };

  return Room;
};
