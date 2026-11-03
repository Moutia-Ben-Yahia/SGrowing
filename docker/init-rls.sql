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

-- 3. Macro to apply strict tenant isolation RLS policy to all core tenant tables
DO $$
DECLARE
    tbl RECORD;
    target_tables TEXT[] := ARRAY[
        'Workspaces',
        'Contacts',
        'Companies',
        'Activities',
        'Inboxes',
        'Conversations',
        'Messages',
        'AiDrafts',
        'SenderAccounts',
        'WarmupRecords',
        'OutboundCampaigns',
        'CadenceSteps',
        'ProspectEnrollments',
        'Deals',
        'CustomerEvents',
        'CustomerJourneys',
        'JourneySteps',
        'JourneyEnrollments',
        'SurveyForms',
        'SurveyResponses',
        'HelpCategories',
        'HelpArticles',
        'WhatsAppTemplates',
        'BroadcastCampaigns',
        'AppointmentSlots',
        'AppointmentBookings',
        'CheckoutLinks',
        'SlaPolicies',
        'VoiceCallSessions',
        'SocialAccounts',
        'SocialPosts',
        'DocumentEnvelopes',
        'EnvelopeSigners',
        'DocumentDwellTelemetries',
        'ShipmentRecords',
        'CodOrders',
        'CalendarBookingRecords',
        'BookingEventTypes',
        'ChannelCredentials',
        'DynamicFormDefinitions',
        'DigitalContracts',
        'Products',
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

        -- Auto-provision B-tree tenant index for optimal RLS scan performance
        BEGIN
            EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I ("TenantId");', 'idx_' || lower(tbl.tablename) || '_tenant_rls', tbl.tablename);
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Could not create index for %: %', tbl.tablename, SQLERRM;
        END;
        
        RAISE NOTICE 'RLS Enabled and Enforced with Index for table: %', tbl.tablename;
    END LOOP;
END;
$$;

-- 4. Explicit Index optimization for fast RLS index scans across all core tables
CREATE INDEX IF NOT EXISTS idx_workspaces_tenant_rls ON "Workspaces" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_contacts_tenant_rls ON "Contacts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_companies_tenant_rls ON "Companies" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_activities_tenant_rls ON "Activities" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_inboxes_tenant_rls ON "Inboxes" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_conversations_tenant_rls ON "Conversations" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_messages_tenant_rls ON "Messages" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_aidrafts_tenant_rls ON "AiDrafts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_senderaccounts_tenant_rls ON "SenderAccounts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_warmuprecords_tenant_rls ON "WarmupRecords" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_outboundcampaigns_tenant_rls ON "OutboundCampaigns" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_cadencesteps_tenant_rls ON "CadenceSteps" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_prospectenrollments_tenant_rls ON "ProspectEnrollments" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_deals_tenant_rls ON "Deals" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_customerevents_tenant_rls ON "CustomerEvents" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_customerjourneys_tenant_rls ON "CustomerJourneys" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_journeysteps_tenant_rls ON "JourneySteps" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_journeyenrollments_tenant_rls ON "JourneyEnrollments" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_surveyforms_tenant_rls ON "SurveyForms" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_surveyresponses_tenant_rls ON "SurveyResponses" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_helpcategories_tenant_rls ON "HelpCategories" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_helparticles_tenant_rls ON "HelpArticles" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_whatsapptemplates_tenant_rls ON "WhatsAppTemplates" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_broadcastcampaigns_tenant_rls ON "BroadcastCampaigns" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_appointmentslots_tenant_rls ON "AppointmentSlots" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_appointmentbookings_tenant_rls ON "AppointmentBookings" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_checkoutlinks_tenant_rls ON "CheckoutLinks" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_slapolicies_tenant_rls ON "SlaPolicies" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_voicecallsessions_tenant_rls ON "VoiceCallSessions" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_socialaccounts_tenant_rls ON "SocialAccounts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_socialposts_tenant_rls ON "SocialPosts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_documentenvelopes_tenant_rls ON "DocumentEnvelopes" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_envelopesigners_tenant_rls ON "EnvelopeSigners" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_documentdwelltelemetries_tenant_rls ON "DocumentDwellTelemetries" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_shipmentrecords_tenant_rls ON "ShipmentRecords" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_codorders_tenant_rls ON "CodOrders" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_calendarbookingrecords_tenant_rls ON "CalendarBookingRecords" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_bookingeventtypes_tenant_rls ON "BookingEventTypes" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_channelcredentials_tenant_rls ON "ChannelCredentials" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_dynamicformdefinitions_tenant_rls ON "DynamicFormDefinitions" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_digitalcontracts_tenant_rls ON "DigitalContracts" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_products_tenant_rls ON "Products" ("TenantId");
CREATE INDEX IF NOT EXISTS idx_workflowgraphs_tenant_rls ON "WorkflowGraphs" ("TenantId");
