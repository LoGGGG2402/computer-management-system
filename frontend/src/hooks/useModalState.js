import { useState, useCallback } from "react";

/**
 * Custom hook to manage modal state.
 * @param {string} [initialAction='create'] - The initial action type ('create' or 'edit').
 * @returns {object} Modal state and handler functions.
 */
export const useModalState = (initialAction = "create") => {
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [selectedItem, setSelectedItem] = useState(null);
  const [modalAction, setModalAction] = useState(initialAction);

  /**
   * Opens the modal.
   * @param {string} action - The action type ('create' or 'edit').
   * @param {object|null} [item=null] - The item to be edited (if action is 'edit').
   */
  const openModal = useCallback((action, item = null) => {
    setModalAction(action);
    setSelectedItem(item);
    setIsModalVisible(true);
  }, []);

  /**
   * Closes the modal and resets state.
   */
  const closeModal = useCallback(() => {
    setIsModalVisible(false);
    setSelectedItem(null);
  }, []);

  return {
    isModalVisible,
    selectedItem,
    modalAction,
    openModal,
    closeModal,
    setIsModalVisible,
    setSelectedItem,
    setModalAction,
  };
};
