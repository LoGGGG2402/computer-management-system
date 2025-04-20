const { v4: uuidv4 } = require('uuid');

/**
 * AgentVersion model represents a specific software version of the agent client.
 * It tracks version information, file metadata, and deployment status in the system.
 * 
 * @module AgentVersion
 * @typedef {Object} AgentVersion
 * @property {string} id - Unique UUID identifier for the agent version
 * @property {string} version - Semantic version string (e.g., '1.2.3') that follows SemVer convention
 * @property {string} checksum_sha256 - SHA-256 hash of the agent package file for integrity verification
 * @property {string} download_url - URL path where agents can download this version package
 * @property {string|null} notes - Release notes describing changes, fixes, and features in this version
 * @property {boolean} is_stable - Flag indicating if this is considered a stable production release
 * @property {string} file_path - Server filesystem path where the agent package is stored
 * @property {number} file_size - Size of the agent package file in bytes
 * @property {Date} created_at - Timestamp when this version was first created in the system
 * @property {Date} updated_at - Timestamp when this version was last modified
 */
module.exports = (sequelize, DataTypes) => {
  const AgentVersion = sequelize.define('AgentVersion', {
    id: {
      type: DataTypes.UUID,
      primaryKey: true,
      defaultValue: () => uuidv4()
    },
    version: {
      type: DataTypes.STRING(50),
      allowNull: false,
      unique: true,
      validate: {
        notEmpty: true
      }
    },
    checksum_sha256: {
      type: DataTypes.STRING(64),
      allowNull: false,
      validate: {
        notEmpty: true,
        is: /^[a-f0-9]{64}$/i // SHA-256 is 64 hex characters
      }
    },
    download_url: {
      type: DataTypes.STRING(255),
      allowNull: false
    },
    notes: {
      type: DataTypes.TEXT,
      allowNull: true
    },
    is_stable: {
      type: DataTypes.BOOLEAN,
      defaultValue: false
    },
    file_path: {
      type: DataTypes.STRING(255),
      allowNull: false
    },
    file_size: {
      type: DataTypes.INTEGER,
      allowNull: false
    }
  }, {
    tableName: 'agent_versions',
    timestamps: true,
    underscored: true,
    createdAt: 'created_at',
    updatedAt: 'updated_at'
  });

  return AgentVersion;
};