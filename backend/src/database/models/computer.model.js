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
      allowNull: true 
    },
    last_update: {
      type: DataTypes.DATE
    },
    os_info: {
      type: DataTypes.STRING(255)
    },
    total_ram: {
      type: DataTypes.BIGINT
    },
    cpu_info: {
      type: DataTypes.STRING(255)
    },
    total_disk_space: {
      type: DataTypes.BIGINT,
      allowNull: true
    },
    gpu_info: {
      type: DataTypes.STRING(255),
      allowNull: true
    },
    errors: {
      type: DataTypes.JSONB,
      defaultValue: '[]'
    },
    have_active_errors: {
      type: DataTypes.BOOLEAN,
      defaultValue: false
    },
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