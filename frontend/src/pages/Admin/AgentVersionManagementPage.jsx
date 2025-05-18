import React, { useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/index';
import { fetchAgentVersions, selectAgentVersions, selectAdminLoading } from '../../app/index';
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
  const dispatch = useAppDispatch();
  const versions = useAppSelector(selectAgentVersions);
  const loading = useAppSelector(selectAdminLoading);

  useEffect(() => {
    dispatch(fetchAgentVersions());
  }, [dispatch]);

  return (
    <div className="agent-version-management-page">
      <AgentVersionManagement versions={versions} loading={loading} />
    </div>
  );
};

export default AgentVersionManagementPage;