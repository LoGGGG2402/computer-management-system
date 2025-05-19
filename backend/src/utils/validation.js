const constants = {
  AGENT_ID_MIN_LENGTH: 8,
  AGENT_ID_MAX_LENGTH: 36,
  ROOM_NAME_MIN_LENGTH: 3,
  ROOM_NAME_MAX_LENGTH: 100,
  MFA_CODE_LENGTH: 6,
  GPU_INFO_MAX_LENGTH: 500,
  CPU_INFO_MAX_LENGTH: 500,
  OS_INFO_MAX_LENGTH: 200,
  ERROR_TYPE_MIN_LENGTH: 2,
  ERROR_TYPE_MAX_LENGTH: 50,
  ERROR_MESSAGE_MIN_LENGTH: 5,
  ERROR_MESSAGE_MAX_LENGTH: 255,
  ERROR_DETAILS_MAX_SIZE_KB: 2, // 2KB
  FILENAME_REGEX: /^[a-zA-Z0-9._-]+$/,
  SEMANTIC_VERSION_REGEX:
    /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$/, // Basic semver regex

  // Frontend API constants
  USERNAME_MIN_LENGTH: 3,
  USERNAME_MAX_LENGTH: 50,
  USERNAME_REGEX: /^[a-zA-Z][a-zA-Z0-9_-]{2,49}$/,
  PASSWORD_MIN_LENGTH: 8,
  PASSWORD_MAX_LENGTH: 128,
  PASSWORD_REGEX:
    /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,128}$/,
  ROOM_DESCRIPTION_MAX_LENGTH: 1000,
  LAYOUT_MIN_ROWS_COLS: 1,
  LAYOUT_MAX_ROWS_COLS: 50,
  USER_IDS_MAX_ASSIGN: 100,
  COMPUTER_NAME_SEARCH_MIN_LENGTH: 2,
  RESOLUTION_NOTES_MIN_LENGTH: 5,
  RESOLUTION_NOTES_MAX_LENGTH: 1000,
  AGENT_VERSION_NOTES_MAX_LENGTH: 2000,
  SHA256_CHECKSUM_LENGTH: 64,
  UUID_REGEX:
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/,
  PAGE_QUERY_PARAM_MIN: 1,
  LIMIT_QUERY_PARAM_MIN: 1,
  LIMIT_QUERY_PARAM_MAX: 100,
  ERROR_TYPE_FRONTEND_ENUM: [
    "hardware",
    "software",
    "network",
    "peripheral",
    "other",
  ],
  AGENT_UPDATE_ERROR_TYPES: [
    "DownloadFailed",
    "ChecksumMismatch",
    "ExtractionFailed",
    "UpdateLaunchFailed",
    "StartAgentFailed",
    "UpdateGeneralFailure",
  ],
};

/**
 * Validates an Agent ID.
 * @param {string} agentId - The agent ID to validate.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateAgentId(agentId) {
  if (typeof agentId !== "string") {
    return "Agent ID must be a string.";
  }
  if (
    agentId.length < constants.AGENT_ID_MIN_LENGTH ||
    agentId.length > constants.AGENT_ID_MAX_LENGTH
  ) {
    return `Agent ID must be between ${constants.AGENT_ID_MIN_LENGTH} and ${constants.AGENT_ID_MAX_LENGTH} characters.`;
  }
  // Add more specific regex if needed, e.g., for UUID format or specific character sets.
  return null;
}

/**
 * Validates a Room Name.
 * @param {string} roomName - The room name to validate.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateRoomName(roomName) {
  if (typeof roomName !== "string") {
    return "Room Name must be a string.";
  }
  if (
    roomName.length < constants.ROOM_NAME_MIN_LENGTH ||
    roomName.length > constants.ROOM_NAME_MAX_LENGTH
  ) {
    return `Room Name must be between ${constants.ROOM_NAME_MIN_LENGTH} and ${constants.ROOM_NAME_MAX_LENGTH} characters.`;
  }
  // Regex for allowed characters can be added here if specified in API docs,
  // e.g., frontend_api.md specifies: ^[\w\s.,;:!?()-]{3,100}$
  const roomNameRegex = /^[\w\s.,;:!?()-]{3,100}$/;
  if (!roomNameRegex.test(roomName)) {
    return "Room Name contains invalid characters.";
  }
  return null;
}

/**
 * Validates a position coordinate (X or Y).
 * @param {any} coord - The coordinate value to validate.
 * @param {string} coordName - The name of the coordinate (e.g., 'posX', 'posY') for error messages.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validatePositionCoordinate(coord, coordName) {
  if (typeof coord !== "number" || !Number.isInteger(coord)) {
    return `${coordName} must be an integer.`;
  }
  if (coord < 0) {
    return `${coordName} must be a non-negative integer.`;
  }
  // Max value validation would typically happen in the service layer against room dimensions.
  return null;
}

/**
 * Validates an MFA code.
 * @param {string} mfaCode - The MFA code to validate.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateMfaCode(mfaCode) {
  if (typeof mfaCode !== "string") {
    return "MFA code must be a string.";
  }
  if (mfaCode.length !== constants.MFA_CODE_LENGTH) {
    return `MFA code must be ${constants.MFA_CODE_LENGTH} characters long.`;
  }
  if (!/^[a-zA-Z0-9]+$/.test(mfaCode)) {
    // API doc says alphanumeric, case-insensitive
    return "MFA code must be alphanumeric.";
  }
  return null;
}

/**
 * Validates total disk space or RAM.
 * @param {any} value - The value for disk space or RAM.
 * @param {string} fieldName - The name of the field (e.g., 'Total Disk Space', 'Total RAM').
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validatePositiveInteger(value, fieldName) {
  if (typeof value !== "number" || !Number.isInteger(value)) {
    return `${fieldName} must be an integer.`;
  }
  if (value <= 0) {
    return `${fieldName} must be a positive integer.`;
  }
  return null;
}

/**
 * Validates a string field for maximum length.
 * @param {string} value - The string value.
 * @param {number} maxLength - The maximum allowed length.
 * @param {string} fieldName - The name of the field.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateMaxLength(value, maxLength, fieldName) {
  if (typeof value !== "string") {
    return `${fieldName} must be a string.`;
  }
  if (value.length > maxLength) {
    return `${fieldName} must not exceed ${maxLength} characters.`;
  }
  return null;
}

/**
 * Validates an error type for agent error reports.
 * @param {string} errorType - The error type string.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateAgentErrorType(errorType) {
  if (typeof errorType !== "string") {
    return "Error type must be a string.";
  }
  if (
    errorType.length < constants.ERROR_TYPE_MIN_LENGTH ||
    errorType.length > constants.ERROR_TYPE_MAX_LENGTH
  ) {
    return `Error type must be between ${constants.ERROR_TYPE_MIN_LENGTH} and ${constants.ERROR_TYPE_MAX_LENGTH} characters.`;
  }
  // For update errors, check against the enum
  // This part can be enhanced if the context of 'update error' is known here
  // const updateErrorTypes = constants.AGENT_UPDATE_ERROR_TYPES;
  // if (isUpdateErrorContext && !updateErrorTypes.includes(errorType)) {
  //   return `Invalid error type for update. Must be one of: ${updateErrorTypes.join(', ')}.`;
  // }
  return null;
}

/**
 * Validates an error message for agent error reports.
 * @param {string} errorMessage - The error message string.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateAgentErrorMessage(errorMessage) {
  if (typeof errorMessage !== "string") {
    return "Error message must be a string.";
  }
  if (
    errorMessage.length < constants.ERROR_MESSAGE_MIN_LENGTH ||
    errorMessage.length > constants.ERROR_MESSAGE_MAX_LENGTH
  ) {
    return `Error message must be between ${constants.ERROR_MESSAGE_MIN_LENGTH} and ${constants.ERROR_MESSAGE_MAX_LENGTH} characters.`;
  }
  return null;
}

/**
 * Validates error details object size.
 * @param {object} details - The details object.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateErrorDetailsSize(details) {
  if (details && typeof details === "object") {
    const detailsSize = Buffer.byteLength(JSON.stringify(details), "utf8");
    if (detailsSize > constants.ERROR_DETAILS_MAX_SIZE_KB * 1024) {
      return `Details object exceeds maximum size of ${constants.ERROR_DETAILS_MAX_SIZE_KB}KB.`;
    }
  } else if (details !== undefined && details !== null) {
    return "Details must be an object or null/undefined.";
  }
  return null;
}

/**
 * Validates a semantic version string.
 * @param {string} version - The version string.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateSemanticVersion(version) {
  if (typeof version !== "string") {
    return "Version must be a string.";
  }
  if (!constants.SEMANTIC_VERSION_REGEX.test(version)) {
    return "Version must follow semantic versioning format (e.g., X.Y.Z).";
  }
  return null;
}

/**
 * Validates a filename based on a regex.
 * @param {string} filename - The filename string.
 * @returns {string|null} An error message if validation fails, otherwise null.
 */
function validateFilename(filename) {
  if (typeof filename !== "string") {
    return "Filename must be a string.";
  }
  if (!constants.FILENAME_REGEX.test(filename)) {
    return "Filename contains invalid characters.";
  }
  return null;
}

// --- Frontend API Validations ---

/**
 * Validates a username.
 * @param {string} username - The username.
 * @returns {string|null} Error message or null.
 */
function validateUsername(username) {
  if (typeof username !== "string") return "Username must be a string.";
  if (
    username.length < constants.USERNAME_MIN_LENGTH ||
    username.length > constants.USERNAME_MAX_LENGTH
  ) {
    return `Username must be between ${constants.USERNAME_MIN_LENGTH} and ${constants.USERNAME_MAX_LENGTH} characters.`;
  }
  if (!constants.USERNAME_REGEX.test(username)) {
    return "Username must start with a letter and contain only alphanumeric characters, underscores, or hyphens.";
  }
  return null;
}

/**
 * Validates a password.
 * @param {string} password - The password.
 * @returns {string|null} Error message or null.
 */
function validatePassword(password) {
  if (typeof password !== "string") return "Password must be a string.";
  if (
    password.length < constants.PASSWORD_MIN_LENGTH ||
    password.length > constants.PASSWORD_MAX_LENGTH
  ) {
    return `Password must be between ${constants.PASSWORD_MIN_LENGTH} and ${constants.PASSWORD_MAX_LENGTH} characters.`;
  }
  if (!constants.PASSWORD_REGEX.test(password)) {
    return "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.";
  }
  return null;
}

/**
 * Validates a user role.
 * @param {string} role - The role.
 * @returns {string|null} Error message or null.
 */
function validateUserRole(role) {
  if (typeof role !== "string") return "Role must be a string.";
  if (!["admin", "user"].includes(role.toLowerCase())) {
    return "Role must be either 'admin' or 'user'.";
  }
  return null;
}

/**
 * Validates an 'is_active' flag.
 * @param {any} isActive - The flag value.
 * @returns {string|null} Error message or null.
 */
function validateIsActiveFlag(isActive) {
  if (typeof isActive !== "boolean") {
    return "Is_active flag must be a boolean (true or false).";
  }
  return null;
}

/**
 * Validates page query parameter.
 * @param {any} page - The page value.
 * @returns {string|null} Error message or null.
 */
function validatePageQueryParam(page) {
  const pageNum = parseInt(page, 10);
  if (isNaN(pageNum) || pageNum < constants.PAGE_QUERY_PARAM_MIN) {
    return `Page must be an integer greater than or equal to ${constants.PAGE_QUERY_PARAM_MIN}.`;
  }
  return null;
}

/**
 * Validates limit query parameter.
 * @param {any} limit - The limit value.
 * @returns {string|null} Error message or null.
 */
function validateLimitQueryParam(limit) {
  const limitNum = parseInt(limit, 10);
  if (
    isNaN(limitNum) ||
    limitNum < constants.LIMIT_QUERY_PARAM_MIN ||
    limitNum > constants.LIMIT_QUERY_PARAM_MAX
  ) {
    return `Limit must be an integer between ${constants.LIMIT_QUERY_PARAM_MIN} and ${constants.LIMIT_QUERY_PARAM_MAX}.`;
  }
  return null;
}

/**
 * Validates username search query parameter.
 * @param {string} usernameSearch - The username search string.
 * @returns {string|null} Error message or null.
 */
function validateUsernameSearchQuery(usernameSearch) {
  if (typeof usernameSearch !== "string")
    return "Username search term must be a string.";
  if (
    usernameSearch.length > 0 &&
    usernameSearch.length < constants.COMPUTER_NAME_SEARCH_MIN_LENGTH
  ) {
    // Assuming same min length for user search
    return `Username search term must be at least ${constants.COMPUTER_NAME_SEARCH_MIN_LENGTH} characters if provided.`;
  }
  return null;
}

/**
 * Validates room description.
 * @param {string} description - The room description.
 * @returns {string|null} Error message or null.
 */
function validateRoomDescription(description) {
  if (typeof description !== "string") return "Description must be a string.";
  if (description.length > constants.ROOM_DESCRIPTION_MAX_LENGTH) {
    return `Description must not exceed ${constants.ROOM_DESCRIPTION_MAX_LENGTH} characters.`;
  }
  return null;
}

/**
 * Validates layout dimension (rows or columns).
 * @param {any} dimension - The dimension value.
 * @param {string} dimensionName - Name of the dimension ('rows' or 'columns').
 * @returns {string|null} Error message or null.
 */
function validateLayoutDimension(dimension, dimensionName) {
  if (typeof dimension !== "number" || !Number.isInteger(dimension)) {
    return `${dimensionName} must be an integer.`;
  }
  if (
    dimension < constants.LAYOUT_MIN_ROWS_COLS ||
    dimension > constants.LAYOUT_MAX_ROWS_COLS
  ) {
    return `${dimensionName} must be between ${constants.LAYOUT_MIN_ROWS_COLS} and ${constants.LAYOUT_MAX_ROWS_COLS}.`;
  }
  return null;
}

/**
 * Validates an array of User IDs for assignment.
 * @param {any[]} userIds - The array of user IDs.
 * @returns {string|null} Error message or null.
 */
function validateUserIdsArray(userIds) {
  if (!Array.isArray(userIds)) {
    return "User IDs must be an array.";
  }
  if (userIds.length === 0) {
    return "User IDs array must not be empty.";
  }
  if (userIds.length > constants.USER_IDS_MAX_ASSIGN) {
    return `Cannot assign more than ${constants.USER_IDS_MAX_ASSIGN} users at a time.`;
  }
  const uniqueIds = new Set();
  for (const id of userIds) {
    if (typeof id !== "number" || !Number.isInteger(id) || id <= 0) {
      return "Each User ID in the array must be a positive integer.";
    }
    if (uniqueIds.has(id)) {
      return "User IDs array must contain unique values.";
    }
    uniqueIds.add(id);
  }
  return null;
}

/**
 * Validates computer name search query parameter.
 * @param {string} nameSearch - The name search string.
 * @returns {string|null} Error message or null.
 */
function validateComputerNameSearchQuery(nameSearch) {
  if (typeof nameSearch !== "string")
    return "Name search term must be a string.";
  if (
    nameSearch.length > 0 &&
    nameSearch.length < constants.COMPUTER_NAME_SEARCH_MIN_LENGTH
  ) {
    return `Name search term must be at least ${constants.COMPUTER_NAME_SEARCH_MIN_LENGTH} characters if provided.`;
  }
  return null;
}

/**
 * Validates computer status query parameter.
 * @param {string} status - The status string.
 * @returns {string|null} Error message or null.
 */
function validateComputerStatusQuery(status) {
  if (typeof status !== "string") return "Status must be a string.";
  if (!["online", "offline"].includes(status.toLowerCase())) {
    return "Status must be either 'online' or 'offline'.";
  }
  return null;
}

/**
 * Validates 'has_errors' query parameter.
 * @param {string} hasErrors - The has_errors string.
 * @returns {string|null} Error message or null.
 */
function validateHasErrorsQuery(hasErrors) {
  if (typeof hasErrors !== "string")
    return "has_errors filter must be a string.";
  if (!["true", "false"].includes(hasErrors.toLowerCase())) {
    return "has_errors filter must be 'true' or 'false'.";
  }
  return null;
}

/**
 * Validates frontend error type.
 * @param {string} errorType - The error type string.
 * @returns {string|null} Error message or null.
 */
function validateFrontendErrorType(errorType) {
  if (typeof errorType !== "string") return "Error type must be a string.";
  if (
    errorType.length < constants.ERROR_TYPE_MIN_LENGTH ||
    errorType.length > constants.ERROR_TYPE_MAX_LENGTH
  ) {
    return `Error type must be between ${constants.ERROR_TYPE_MIN_LENGTH} and ${constants.ERROR_TYPE_MAX_LENGTH} characters.`;
  }
  if (!constants.ERROR_TYPE_FRONTEND_ENUM.includes(errorType.toLowerCase())) {
    return `Error type must be one of: ${constants.ERROR_TYPE_FRONTEND_ENUM.join(
      ", "
    )}.`;
  }
  return null;
}

/**
 * Validates resolution notes.
 * @param {string} notes - The resolution notes.
 * @returns {string|null} Error message or null.
 */
function validateResolutionNotes(notes) {
  if (typeof notes !== "string") return "Resolution notes must be a string.";
  if (
    notes.trim().length < constants.RESOLUTION_NOTES_MIN_LENGTH ||
    notes.length > constants.RESOLUTION_NOTES_MAX_LENGTH
  ) {
    return `Resolution notes must be between ${constants.RESOLUTION_NOTES_MIN_LENGTH} and ${constants.RESOLUTION_NOTES_MAX_LENGTH} characters and not be empty or only whitespace.`;
  }
  return null;
}

/**
 * Validates agent version notes.
 * @param {string} notes - The agent version notes.
 * @returns {string|null} Error message or null.
 */
function validateAgentVersionNotes(notes) {
  if (typeof notes !== "string") return "Agent version notes must be a string.";
  if (notes.length > constants.AGENT_VERSION_NOTES_MAX_LENGTH) {
    return `Agent version notes must not exceed ${constants.AGENT_VERSION_NOTES_MAX_LENGTH} characters.`;
  }
  return null;
}

/**
 * Validates SHA256 checksum format.
 * @param {string} checksum - The checksum string.
 * @returns {string|null} Error message or null.
 */
function validateSha256Checksum(checksum) {
  if (typeof checksum !== "string") return "Checksum must be a string.";
  if (
    checksum.length !== constants.SHA256_CHECKSUM_LENGTH ||
    !/^[a-f0-9]{64}$/i.test(checksum)
  ) {
    return "Checksum must be a 64-character hexadecimal string.";
  }
  return null;
}

/**
 * Validates UUID format.
 * @param {string} uuid - The UUID string.
 * @returns {string|null} Error message or null.
 */
function validateUuid(uuid) {
  if (typeof uuid !== "string") return "ID must be a string.";
  if (!constants.UUID_REGEX.test(uuid)) {
    return "ID must be a valid UUID.";
  }
  return null;
}

/**
 * Validates a positive integer for IDs (e.g., computerId, roomId).
 * @param {any} idValue - The ID value.
 * @param {string} idName - The name of the ID (e.g., 'Computer ID').
 * @returns {string|null} Error message or null.
 */
function validatePositiveIntegerId(idValue, idName) {
  const numValue = parseInt(idValue, 10);
  if (
    isNaN(numValue) ||
    (typeof idValue !== "number" && typeof idValue !== "string") ||
    numValue <= 0 ||
    (typeof idValue === "string" &&
      String(numValue) !== idValue.trim() &&
      !Number.isInteger(parseFloat(idValue)))
  ) {
    if (typeof idValue === "number" && !Number.isInteger(idValue)) {
      return `${idName} must be an integer.`;
    }
    return `${idName} must be a positive integer.`;
  }
  return null;
}

/**
 * Middleware factory for validating request body, query, or params using a schema.
 * This is a conceptual example. For robust validation, use libraries like express-validator or Joi.
 *
 * @param {object} schema - An object where keys are field names and values are validation functions.
 * @param {'body' | 'query' | 'params'} source - Where to look for the fields ('body', 'query', 'params').
 * @returns {function} Express middleware function.
 */
const validateRequest = (schema, source = "body") => {
  return (req, res, next) => {
    const errors = [];
    const dataToValidate = req[source];

    for (const field in schema) {
      const value = dataToValidate[field];
      const validator = schema[field];
      // Check for presence if validator is an object with 'required: true'
      if (typeof validator === "object" && validator.required) {
        if (value === undefined || value === null || value === "") {
          errors.push({ field, message: `${field} is required.` });
          continue; // Skip further validation for this field if required and missing
        }
        // If required and present, use the 'validate' function from the object
        if (typeof validator.validate === "function") {
          const errorMessage = validator.validate(value, field);
          if (errorMessage) {
            errors.push({ field, message: errorMessage });
          }
        }
      } else if (typeof validator === "function") {
        // If validator is a direct function, call it only if value is present
        // This makes fields optional by default unless wrapped in the object with 'required: true'
        if (value !== undefined && value !== null && value !== "") {
          const errorMessage = validator(value, field);
          if (errorMessage) {
            errors.push({ field, message: errorMessage });
          }
        }
      }
    }

    if (errors.length > 0) {
      return res.status(400).json({
        status: "error",
        message: errors[0].message,
        errors,
      });
    }
    next();
  };
};

module.exports = {
  constants,
  validateAgentId,
  validateRoomName,
  validatePositionCoordinate,
  validateMfaCode,
  validatePositiveInteger,
  validateMaxLength,
  validateAgentErrorType,
  validateAgentErrorMessage,
  validateErrorDetailsSize,
  validateSemanticVersion,
  validateFilename,
  validateUsername,
  validatePassword,
  validateUserRole,
  validateIsActiveFlag,
  validatePageQueryParam,
  validateLimitQueryParam,
  validateUsernameSearchQuery,
  validateRoomDescription,
  validateLayoutDimension,
  validateUserIdsArray,
  validateComputerNameSearchQuery,
  validateComputerStatusQuery,
  validateHasErrorsQuery,
  validateFrontendErrorType,
  validateResolutionNotes,
  validateAgentVersionNotes,
  validateSha256Checksum,
  validateUuid,
  validatePositiveIntegerId,
  validateRequest, // Export the middleware factory
};
