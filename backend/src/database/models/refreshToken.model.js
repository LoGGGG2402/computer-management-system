module.exports = (sequelize, DataTypes) => {
  const RefreshToken = sequelize.define('RefreshToken', {
    id: {
      type: DataTypes.INTEGER,
      autoIncrement: true,
      primaryKey: true,
    },
    user_id: {
      type: DataTypes.INTEGER,
      allowNull: false,
      references: {
        model: 'users',
        key: 'id',
      },
      onDelete: 'CASCADE',
    },
    selector: {
      type: DataTypes.STRING(255),
      allowNull: false,
      unique: true, 
    },
    hashed_verifier: {
      type: DataTypes.STRING(128), 
      allowNull: false,
    },
    expires_at: {
      type: DataTypes.DATE,
      allowNull: false,
    },
    issued_at: {
      type: DataTypes.DATE,
      allowNull: false,
      defaultValue: DataTypes.NOW,
    }
  }, {
    timestamps: false,
    tableName: 'refresh_tokens',
    indexes: [
      {
        unique: true,
        fields: ['selector']
      }
    ]
  });

  RefreshToken.associate = (models) => {
    RefreshToken.belongsTo(models.User, {
      foreignKey: 'user_id',
      as: 'user',
    });
  };

  return RefreshToken;
};
