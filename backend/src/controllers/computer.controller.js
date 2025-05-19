const computerService = require("../services/computer.service");
const logger = require("../utils/logger");
const validationUtils = require("../utils/validation");

/**
 * Controller for computer management operations
 */
class ComputerController {
  /**
   * Get all computers with pagination and filters
   * @param {Object} req - Express request object
   * @param {Object} req.query - Query parameters
   * @param {number} [req.query.page=1] - Page number for pagination
   * @param {number} [req.query.limit=10] - Number of computers per page
   * @param {string} [req.query.name] - Filter by computer name (partial match)
   * @param {number} [req.query.roomId] - Filter by room ID
   * @param {string} [req.query.status] - Filter by status ('online'/'offline')
   * @param {boolean|string} [req.query.has_errors] - Filter to show only computers with errors
   * @param {Object} req.user - Current authenticated user
   * @param {number} req.user.id - User ID
   * @param {string} req.user.role - User role ('admin'/'user')
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Pagination result with:
   *     - total {number} - Total number of computers matching criteria
   *     - currentPage {number} - Current page number
   *     - totalPages {number} - Total number of pages
   *     - computers {Array<Object>} - Array of computer objects:
   *       - id {number} - Computer ID
   *       - name {string} - Computer name
   *       - status {string} - Computer status ('online'/'offline')
   *       - have_active_errors {boolean} - Whether computer has active errors
   *       - last_update {string} - When computer was last updated (ISO-8601 format)
   *       - room_id {number} - ID of the room this computer belongs to
   *       - pos_x {number} - X position in room grid
   *       - pos_y {number} - Y position in room grid
   *       - os_info {Object} - Operating system information
   *       - cpu_info {Object} - CPU information
   *       - gpu_info {Object} - GPU information
   *       - total_ram {number} - Total RAM in GB
   *       - total_disk_space {number} - Total disk space in GB
   *       - room {Object} - Associated room information:
   *         - id {number} - Room ID
   *         - name {string} - Room name
   *   - message {string} - Error message (only if status is 'error')
   */
  async getAllComputers(req, res) {
    try {
      const errors = [];

      if (req.query.page !== undefined) {
        const pageError = validationUtils.validatePageQueryParam(
          req.query.page
        );
        if (pageError) {
          errors.push({ field: "page", message: pageError });
        }
      }

      if (req.query.limit !== undefined) {
        const limitError = validationUtils.validateLimitQueryParam(
          req.query.limit
        );
        if (limitError) {
          errors.push({ field: "limit", message: limitError });
        }
      }

      if (req.query.name) {
        const nameError = validationUtils.validateComputerNameSearchQuery(
          req.query.name
        );
        if (nameError) {
          errors.push({ field: "name", message: nameError });
        }
      }

      if (req.query.roomId) {
        const roomIdError = validationUtils.validatePositiveIntegerId(
          req.query.roomId,
          "Room ID"
        );
        if (roomIdError) {
          errors.push({ field: "roomId", message: roomIdError });
        }
      }

      if (req.query.status) {
        const statusError = validationUtils.validateComputerStatusQuery(
          req.query.status
        );
        if (statusError) {
          errors.push({ field: "status", message: statusError });
        }
      }

      if (req.query.has_errors) {
        const hasErrorsError = validationUtils.validateHasErrorsQuery(
          req.query.has_errors
        );
        if (hasErrorsError) {
          errors.push({ field: "has_errors", message: hasErrorsError });
        }
      }

      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const page = parseInt(req.query.page) || 1;
      const limit = parseInt(req.query.limit) || 10;

      const filters = {
        name: req.query.name,
        roomId: req.query.roomId ? parseInt(req.query.roomId) : null,
        status: req.query.status,
        has_errors: req.query.has_errors,
      };

      const result = await computerService.getAllComputers(
        page,
        limit,
        filters,
        req.user
      );

      logger.debug(
        `Retrieved ${result.computers.length} computers (page ${page}, total: ${result.total})`,
        {
          userId: req.user?.id,
          filters,
        }
      );

      return res.status(200).json({
        status: "success",
        data: result,
      });
    } catch (error) {
      logger.error("Failed to fetch computers:", {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
        query: req.query,
      });

      return res.status(500).json({
        status: "error",
        message: error.message || "Failed to fetch computers",
      });
    }
  }

  /**
   * Get computer by ID
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.computerId - Computer ID to retrieve
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Computer object with:
   *     - id {number} - Computer ID
   *     - name {string} - Computer name
   *     - status {string} - Computer status ('online'/'offline')
   *     - have_active_errors {boolean} - Whether computer has active errors
   *     - last_update {string} - When computer was last updated (ISO-8601 format)
   *     - room_id {number} - ID of the room this computer belongs to
   *     - pos_x {number} - X position in room grid
   *     - pos_y {number} - Y position in room grid
   *     - os_info {Object} - Operating system information
   *     - cpu_info {Object} - CPU information
   *     - gpu_info {Object} - GPU information
   *     - total_ram {number} - Total RAM in GB
   *     - total_disk_space {number} - Total disk space in GB
   *     - room {Object} - Associated room information:
   *       - id {number} - Room ID
   *       - name {string} - Room name
   *   - message {string} - Error message (only if status is 'error')
   */
  async getComputerById(req, res) {
    try {
      const id = parseInt(req.params.computerId);

      if (!id) {
        logger.debug("Invalid computer ID provided", {
          id: req.params.computerId,
        });
        return res.status(400).json({
          status: "error",
          message: "Computer ID is required",
        });
      }

      const computer = await computerService.getComputerById(id);

      logger.debug(`Retrieved computer details for ID: ${id}`, {
        userId: req.user?.id,
        computerName: computer.name,
      });

      return res.status(200).json({
        status: "success",
        data: computer,
      });
    } catch (error) {
      logger.error(`Failed to get computer with ID ${req.params.computerId}:`, {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      return res.status(404).json({
        status: "error",
        message: error.message || "Computer not found",
      });
    }
  }

  /**
   * Delete a computer
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.computerId - Computer ID to delete
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - message {string} - Success or error message
   */
  async deleteComputer(req, res) {
    try {
      const id = parseInt(req.params.computerId);

      if (!id) {
        logger.debug("Invalid computer ID provided for deletion", {
          id: req.params.computerId,
        });
        return res.status(400).json({
          status: "error",
          message: "Computer ID is required",
        });
      }

      await computerService.deleteComputer(id);

      logger.info(`Computer ID: ${id} deleted by user ID: ${req.user?.id}`);

      return res.status(200).json({
        status: "success",
        message: "Computer deleted successfully",
      });
    } catch (error) {
      logger.error(`Failed to delete computer ID ${req.params.computerId}:`, {
        error: error.message,
        stack: error.stack,
        userId: req.user?.id,
      });

      return res.status(404).json({
        status: "error",
        message: error.message || "Computer not found",
      });
    }
  }

  /**
   * Get errors for a specific computer
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.computerId - Computer ID to get errors for
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Errors object with:
   *     - errors {Array<Object>} - Array of error objects:
   *       - id {number} - Error ID
   *       - error_type {string} - Type/category of the error
   *       - error_message {string} - Human-readable error message
   *       - error_details {Object} - Additional details about the error
   *       - reported_at {string} - When the error was reported (ISO-8601 format)
   *       - resolved {boolean} - Whether the error has been resolved
   *       - resolved_at {string} - When the error was resolved (if applicable) (ISO-8601 format)
   *       - resolution_notes {string} - Notes about how the error was resolved
   *   - message {string} - Error message (only if status is 'error')
   */
  async getComputerErrors(req, res) {
    try {
      const id = parseInt(req.params.computerId);

      if (!id) {
        logger.debug("Invalid computer ID provided for error lookup", {
          id: req.params.computerId,
        });
        return res.status(400).json({
          status: "error",
          message: "Computer ID is required",
        });
      }

      const errors = await computerService.getComputerErrors(id);

      logger.debug(`Retrieved ${errors.length} errors for computer ID: ${id}`, {
        userId: req.user?.id,
      });

      return res.status(200).json({
        status: "success",
        data: { errors },
      });
    } catch (error) {
      logger.error(
        `Failed to get errors for computer ID ${req.params.computerId}:`,
        {
          error: error.message,
          stack: error.stack,
          userId: req.user?.id,
        }
      );

      return res.status(404).json({
        status: "error",
        message: error.message || "Computer not found or no errors available",
      });
    }
  }

  /**
   * Report a new error for a computer
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.computerId - Computer ID to report error for
   * @param {Object} req.body - Request body
   * @param {string} req.body.error_type - Type/category of the error
   * @param {string} req.body.error_message - Human-readable error message
   * @param {Object} [req.body.error_details] - Additional details about the error
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Result object with:
   *     - error {Object} - The created error object:
   *       - id {number} - Error ID
   *       - error_type {string} - Type/category of the error
   *       - error_message {string} - Human-readable error message
   *       - error_details {Object} - Additional details about the error
   *       - reported_at {string} - When the error was reported (ISO-8601 format)
   *       - resolved {boolean} - Whether the error has been resolved (false for new errors)
   *     - computerId {number} - The computer ID associated with this error
   *   - message {string} - Success or error message
   */
  async reportComputerError(req, res) {
    try {
      const id = parseInt(req.params.computerId);
      const { error_type, error_message, error_details } = req.body;
      const errors = [];

      if (!id) {
        logger.debug("Invalid computer ID provided for error reporting", {
          id: req.params.computerId,
        });
        return res.status(400).json({
          status: "error",
          message: "Computer ID is required",
        });
      }

      if (!error_type || !error_message) {
        logger.debug("Missing required error fields", {
          hasErrorType: !!error_type,
          hasErrorMessage: !!error_message,
        });

        return res.status(400).json({
          status: "error",
          message: "Error type and message are required",
        });
      }

      const errorTypeError =
        validationUtils.validateFrontendErrorType(error_type);
      if (errorTypeError) {
        errors.push({ field: "error_type", message: errorTypeError });
      }
      const errorMessageError =
        validationUtils.validateAgentErrorMessage(error_message);
      if (errorMessageError) {
        errors.push({ field: "error_message", message: errorMessageError });
      }
      if (error_details !== undefined) {
        const errorDetailsError =
          validationUtils.validateErrorDetailsSize(error_details);
        if (errorDetailsError) {
          errors.push({ field: "error_details", message: errorDetailsError });
        }
      }
      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const errorData = {
        error_type,
        error_message,
        error_details: error_details || {},
      };

      const result = await computerService.reportComputerError(id, errorData);

      logger.info(
        `Error reported for computer ID ${id}: Error ID ${result.error.id}`,
        {
          errorType: error_type,
          userId: req.user?.id,
        }
      );

      return res.status(201).json({
        status: "success",
        data: result,
        message: "Error reported successfully",
      });
    } catch (error) {
      logger.error(
        `Failed to report error for computer ID ${req.params.computerId}:`,
        {
          error: error.message,
          stack: error.stack,
          userId: req.user?.id,
          errorType: req.body.error_type,
        }
      );

      return res.status(400).json({
        status: "error",
        message: error.message || "Failed to report error",
      });
    }
  }

  /**
   * Resolve a computer error
   * @param {Object} req - Express request object
   * @param {Object} req.params - Route parameters
   * @param {string} req.params.computerId - Computer ID
   * @param {string} req.params.errorId - Error ID to resolve
   * @param {Object} req.body - Request body
   * @param {string} [req.body.resolution_notes] - Notes about how the error was resolved
   * @param {Object} res - Express response object
   * @returns {Object} JSON response with:
   *   - status {string} - 'success' or 'error'
   *   - data {Object} - Result object with:
   *     - error {Object} - The updated error object:
   *       - id {number} - Error ID
   *       - error_type {string} - Type/category of the error
   *       - error_message {string} - Human-readable error message
   *       - error_details {Object} - Additional details about the error
   *       - reported_at {string} - When the error was reported (ISO-8601 format)
   *       - resolved {boolean} - Whether the error has been resolved (true after resolution)
   *       - resolved_at {string} - When the error was resolved (ISO-8601 format)
   *       - resolution_notes {string} - Notes about how the error was resolved
   *     - computerId {number} - The computer ID associated with this error
   *   - message {string} - Success or error message
   */
  async resolveComputerError(req, res) {
    try {
      const computerId = parseInt(req.params.computerId);
      const errorId = parseInt(req.params.errorId);
      const { resolution_notes } = req.body;
      const errors = [];

      if (!computerId || !errorId) {
        logger.debug("Invalid IDs provided for error resolution", {
          computerId: req.params.computerId,
          errorId: req.params.errorId,
        });

        return res.status(400).json({
          status: "error",
          message: "Computer ID and Error ID are required",
        });
      }

      if (resolution_notes !== undefined) {
        const notesError =
          validationUtils.validateResolutionNotes(resolution_notes);
        if (notesError) {
          errors.push({ field: "resolution_notes", message: notesError });
        }
      }
      if (errors.length > 0) {
        return res.status(400).json({
          status: "error",
          message: errors[0].message,
          errors,
        });
      }

      const result = await computerService.resolveComputerError(
        computerId,
        errorId,
        resolution_notes
      );

      logger.info(
        `Error ID: ${errorId} for computer ID: ${computerId} resolved by user ID: ${req.user?.id}`,
        {
          hasNotes: !!resolution_notes,
        }
      );

      return res.status(200).json({
        status: "success",
        data: result,
        message: "Error resolved successfully",
      });
    } catch (error) {
      logger.error(
        `Failed to resolve error ID ${req.params.errorId} for computer ID ${req.params.computerId}:`,
        {
          error: error.message,
          stack: error.stack,
          userId: req.user?.id,
        }
      );

      return res.status(400).json({
        status: "error",
        message: error.message || "Failed to resolve error",
      });
    }
  }
}

module.exports = new ComputerController();
