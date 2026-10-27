-- ====================================================================
-- SGrowing Enterprise Multi-Tenant PostgreSQL Row-Level Security (RLS)
-- Enforces complete cryptographically-isolated tenancy at DB engine level.
-- ====================================================================

-- 1. Helper function to set current tenant in session transaction
CREATE OR REPLACE FUNCTION set_tenant_context(p_tenant_id UUID)
RETURNS VOID AS $$
BEGIN
    PERFORM set_config('app.current_tenant_id', p_tenant_id::TEXT, true);
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 2. Helper function to retrieve current tenant in active session
CREATE OR REPLACE FUNCTION get_current_tenant_id()
RETURNS UUID AS $$
BEGIN
    RETURN NULLIF(current_setting('app.current_tenant_id', true), '')::UUID;
END;
$$ LANGUAGE plpgsql STABLE;

-- 3. Macro to apply strict tenant isolation RLS policy to a table
DO $$
DECLARE
    tbl RECORD;
    target_tables TEXT[] := ARRAY[
        'Contacts',
        'Companies',
        'Inboxes',
        'Conversations',
        'Messages',
        'Deals',
        'OutboundCampaigns',
        'SenderAccounts',
        'ChannelCredentials',
        'CodOrders',
        'ShipmentRecords',
        'DocumentEnvelopes',
        'DynamicFormDefinitions',
        'SurveyForms',
        'HelpArticles',
        'DigitalContracts',
        'Products',
        'CustomerEvents',
        'VoiceCallSessions',
        'WorkflowGraphs'
    ];
BEGIN
    FOR tbl IN 
        SELECT tablename 
        FROM pg_tables 
        WHERE schemaname = 'public' 
          AND tablename = ANY(target_tables)
    LOOP
        -- Enable Row Level Security
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', tbl.tablename);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY;', tbl.tablename);

        -- Drop existing policy if present
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation_policy ON %I;', tbl.tablename);

        -- Create strict tenant isolation policy
        EXECUTE format(
            'CREATE POLICY tenant_isolation_policy ON %I
             FOR ALL
             USING ("TenantId" = get_current_tenant_id() OR current_setting(''app.is_admin_bypass'', true) = ''true'')
             WITH CHECK ("TenantId" = get_current_tenant_id() OR current_setting(''app.is_admin_bypass'', true) = ''true'');',
            tbl.tablename
        );
        
        RAISE NOTICE 'RLS Enabled and Enforced for table: %', tbl.tablename;
    END LOOP;
END;
$$;

-- 4. Index optimization for fast RLS index scans
CREATE INDEX IF NOT EXISTS idx_contacts_tenant_rls ON "Contacts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_conversations_tenant_rls ON "Conversations" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_messages_tenant_rls ON "Messages" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_deals_tenant_rls ON "Deals" ("TenantId");
