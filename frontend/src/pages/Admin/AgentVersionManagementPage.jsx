import React from 'react';
import AgentVersionManagement from '../../components/admin/AgentVersionManagement';

/**
 * Agent Version Management Page Component
 * 
 * This page component renders the AgentVersionManagement component
 * which allows administrators to manage agent versions.
 * 
 * @returns {JSX.Element} The rendered page component
 */
const AgentVersionManagementPage = () => {
  return (
    <div className="agent-version-management-page">
      <AgentVersionManagement />
    </div>
  );
};

export default AgentVersionManagementPage;