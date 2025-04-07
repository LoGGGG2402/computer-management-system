module.exports = (sequelize, DataTypes) => {
  const UserRoomAssignment = sequelize.define('UserRoomAssignment', {
    user_id: {
      type: DataTypes.INTEGER,
      allowNull: false,
      references: {
        model: 'users',
        key: 'id'
      },
      primaryKey: true
    },
    room_id: {
      type: DataTypes.INTEGER,
      allowNull: false,
      references: {
        model: 'rooms',
        key: 'id'
      },
      primaryKey: true
    }
  }, {
    tableName: 'user_room_assignments',
    timestamps: false // No timestamps in this junction table according to the readme
  });

  return UserRoomAssignment;
};