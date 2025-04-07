module.exports = (sequelize, DataTypes) => {
  const Computer = sequelize.define('Computer', {
    id: {
      type: DataTypes.INTEGER,
      autoIncrement: true,
      primaryKey: true
    },
    name: {
      type: DataTypes.STRING(255),
      allowNull: true // NULL initially
    },
    pos_x: {
      type: DataTypes.INTEGER,
      defaultValue: 0
    },
    pos_y: {
      type: DataTypes.INTEGER,
      defaultValue: 0
    },
    ip_address: {
      type: DataTypes.STRING(50)
    },
    unique_agent_id: {
      type: DataTypes.STRING(255),
      allowNull: false,
      unique: true
    },
    agent_token_hash: {
      type: DataTypes.STRING(255),
      allowNull: true // NULL until successful registration
    },
    last_seen: {
      type: DataTypes.DATE
    },
    windows_version: {
      type: DataTypes.STRING(255)
    },
    total_ram: {
      type: DataTypes.BIGINT
    },
    cpu_info: {
      type: DataTypes.STRING(255)
    },
    errors: {
      type: DataTypes.JSONB,
      defaultValue: '[]'
    }
  }, {
    tableName: 'computers',
    timestamps: true,
    underscored: true,
    createdAt: 'created_at',
    updatedAt: 'updated_at'
  });

  Computer.associate = (models) => {
    // Many-to-one relationship with Room
    Computer.belongsTo(models.Room, {
      foreignKey: 'room_id',
      as: 'room'
    });
  };

  return Computer;
};