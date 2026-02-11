CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TYPE collection_sub_type AS ENUM ('calendar_proxy_read', 'calendar_proxy_write', 'default', 'scheduling_inbox', 'scheduling_outbox', 'web_push_subscription');
    CREATE TYPE collection_type AS ENUM ('addressbook', 'calendar', 'collection', 'principal');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE SEQUENCE "EntityFrameworkHiLoSequence" START WITH 1 INCREMENT BY 10 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE __data_migration_history (
        id text NOT NULL,
        created_on timestamp with time zone NOT NULL DEFAULT (now()),
        completed_on timestamp with time zone,
        CONSTRAINT pk___data_migration_history PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE calendar_message (
        id integer NOT NULL,
        uid text NOT NULL,
        sequence integer NOT NULL,
        sender_email text NOT NULL,
        receiver_email text NOT NULL,
        body text NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        processed timestamp with time zone,
        CONSTRAINT pk_calendar_message PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE grant_type (
        id integer NOT NULL,
        name text NOT NULL,
        confers text NOT NULL,
        privileges bit(16) NOT NULL,
        CONSTRAINT pk_grant_type PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE principal_type (
        id integer NOT NULL,
        name text NOT NULL,
        label text NOT NULL,
        CONSTRAINT pk_principal_type PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE trx_journal (
        id integer NOT NULL,
        method text NOT NULL,
        path text NOT NULL,
        request_headers text[] NOT NULL,
        request_body text,
        response_status_code integer,
        response_body text,
        response_error text,
        response_headers text[] NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_trx_journal PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE usr (
        id integer NOT NULL,
        is_active boolean NOT NULL,
        username text NOT NULL,
        email text,
        email_ok timestamp with time zone,
        date_format_type text,
        locale text,
        created timestamp with time zone DEFAULT (now()),
        modified timestamp with time zone DEFAULT (now()),
        closed timestamp with time zone,
        CONSTRAINT pk_usr PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE usr_credential_type (
        id integer NOT NULL,
        name text NOT NULL,
        label text NOT NULL,
        CONSTRAINT pk_usr_credential_type PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE collection (
        id integer NOT NULL,
        owner_id integer NOT NULL,
        parent_id integer,
        uri text NOT NULL,
        permanent_id uuid NOT NULL,
        collection_type collection_type NOT NULL,
        collection_sub_type collection_sub_type NOT NULL,
        parent_container_uri text,
        etag text,
        display_name text,
        timezone text,
        description text,
        color text,
        order_by integer,
        schedule_transparency text,
        principal_type_id integer,
        owner_prohibit bit(16) NOT NULL,
        owner_mask bit(16) NOT NULL,
        authorized_prohibit bit(16) NOT NULL,
        authorized_mask bit(16) NOT NULL,
        global_permit_self bit(16) NOT NULL,
        global_permit bit(16) NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        modified timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_collection PRIMARY KEY (id),
        CONSTRAINT ak_collection_uri UNIQUE (uri),
        CONSTRAINT fk_collection_collection_parent_id FOREIGN KEY (parent_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_collection_principal_type_principal_type_id FOREIGN KEY (principal_type_id) REFERENCES principal_type (id),
        CONSTRAINT fk_collection_usr_owner_id FOREIGN KEY (owner_id) REFERENCES usr (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE usr_credential (
        id integer NOT NULL,
        usr_id integer NOT NULL,
        credential_type_id integer NOT NULL,
        accesskey text NOT NULL,
        secret text,
        locked timestamp with time zone,
        validity tstzrange,
        last_used timestamp with time zone,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        modified timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_usr_credential PRIMARY KEY (id),
        CONSTRAINT fk_usr_credential_usr_credential_type_credential_type_id FOREIGN KEY (credential_type_id) REFERENCES usr_credential_type (id) ON DELETE CASCADE,
        CONSTRAINT fk_usr_credential_usr_usr_id FOREIGN KEY (usr_id) REFERENCES usr (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE collection_group (
        group_id integer NOT NULL,
        member_id integer NOT NULL,
        CONSTRAINT pk_collection_group PRIMARY KEY (group_id, member_id),
        CONSTRAINT fk_collection_group_collection_group_id FOREIGN KEY (group_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_collection_group_collection_member_id FOREIGN KEY (member_id) REFERENCES collection (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE collection_object (
        id integer NOT NULL,
        collection_id integer NOT NULL,
        v_object_type text NOT NULL,
        uri text NOT NULL,
        uid text NOT NULL,
        raw_data text NOT NULL,
        etag text NOT NULL,
        schedule_tag text,
        is_public boolean NOT NULL,
        is_private boolean NOT NULL,
        is_confidential boolean NOT NULL,
        owner_id integer NOT NULL,
        actual_user_id integer NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        modified timestamp with time zone NOT NULL DEFAULT (now()),
        deleted timestamp with time zone,
        CONSTRAINT pk_collection_object PRIMARY KEY (id),
        CONSTRAINT ak_collection_object_uri UNIQUE (uri),
        CONSTRAINT fk_collection_object_collection_collection_id FOREIGN KEY (collection_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_collection_object_usr_actual_user_id FOREIGN KEY (actual_user_id) REFERENCES usr (id) ON DELETE CASCADE,
        CONSTRAINT fk_collection_object_usr_owner_id FOREIGN KEY (owner_id) REFERENCES usr (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE collection_property (
        collection_id integer NOT NULL,
        name text NOT NULL,
        value text NOT NULL,
        modified timestamp with time zone NOT NULL DEFAULT (now()),
        modified_by_id integer NOT NULL,
        CONSTRAINT pk_collection_property PRIMARY KEY (collection_id, name),
        CONSTRAINT fk_collection_property_collection_collection_id FOREIGN KEY (collection_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_collection_property_usr_modified_by_id FOREIGN KEY (modified_by_id) REFERENCES usr (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE grant_relation (
        id integer NOT NULL,
        grantor_id integer NOT NULL,
        grantee_id integer NOT NULL,
        grant_type_id integer NOT NULL,
        privileges bit(16) NOT NULL,
        is_indirect boolean NOT NULL DEFAULT FALSE,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        modified timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_grant_relation PRIMARY KEY (id),
        CONSTRAINT ak_grant_relation_grantor_id_grantee_id UNIQUE (grantor_id, grantee_id),
        CONSTRAINT fk_grant_relation_collection_grantee_id FOREIGN KEY (grantee_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_grant_relation_collection_grantor_id FOREIGN KEY (grantor_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_grant_relation_grant_type_grant_type_id FOREIGN KEY (grant_type_id) REFERENCES grant_type (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE push_subscription (
        id integer NOT NULL,
        subscription_id text NOT NULL,
        user_id integer NOT NULL,
        resource_id integer NOT NULL,
        push_destination_uri text NOT NULL,
        client_public_key_type text,
        client_public_key text,
        auth_secret text,
        content_encoding text,
        fail_counter integer NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        expiration timestamp with time zone NOT NULL,
        last_notification timestamp with time zone,
        CONSTRAINT pk_push_subscription PRIMARY KEY (id),
        CONSTRAINT fk_push_subscription_collection_resource_id FOREIGN KEY (resource_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_push_subscription_usr_user_id FOREIGN KEY (user_id) REFERENCES usr (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE address (
        id integer NOT NULL,
        collection_object_id integer NOT NULL,
        formatted_name text,
        name text,
        nickname text,
        birthday timestamp with time zone,
        card_version text,
        CONSTRAINT pk_address PRIMARY KEY (id),
        CONSTRAINT fk_address_collection_object_collection_object_id FOREIGN KEY (collection_object_id) REFERENCES collection_object (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE calendar (
        id integer NOT NULL,
        collection_object_id integer NOT NULL,
        dtstart timestamp with time zone,
        dtend timestamp with time zone,
        due timestamp with time zone,
        completed timestamp with time zone,
        summary text,
        location text,
        description text,
        priority integer,
        class text,
        transp text,
        rrule text,
        url text,
        percent_complete double precision NOT NULL,
        timezone text,
        status text,
        sequence integer NOT NULL,
        is_scheduling boolean NOT NULL,
        organizer_id integer,
        dtstamp timestamp with time zone,
        created timestamp with time zone,
        last_modified timestamp with time zone,
        first_instance_start timestamp with time zone,
        first_instance_end timestamp with time zone,
        last_instance_start timestamp with time zone,
        last_instance_end timestamp with time zone,
        CONSTRAINT pk_calendar PRIMARY KEY (id),
        CONSTRAINT fk_calendar_collection_object_collection_object_id FOREIGN KEY (collection_object_id) REFERENCES collection_object (id) ON DELETE CASCADE,
        CONSTRAINT fk_calendar_usr_organizer_id FOREIGN KEY (organizer_id) REFERENCES usr (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE sync_journal (
        id uuid NOT NULL,
        collection_id integer NOT NULL,
        collection_object_id integer,
        uri text NOT NULL,
        is_deleted boolean NOT NULL,
        created timestamp with time zone NOT NULL DEFAULT (now()),
        issued timestamp with time zone,
        CONSTRAINT pk_sync_journal PRIMARY KEY (id),
        CONSTRAINT fk_sync_journal_collection_collection_id FOREIGN KEY (collection_id) REFERENCES collection (id) ON DELETE CASCADE,
        CONSTRAINT fk_sync_journal_collection_object_collection_object_id FOREIGN KEY (collection_object_id) REFERENCES collection_object (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE TABLE calendar_attendee (
        id integer NOT NULL,
        calendar_id integer NOT NULL,
        status text,
        role text,
        rsvp boolean,
        participation_status text,
        common_name text,
        e_mail text,
        language text,
        e_mail_state text,
        attendee_state text,
        attendee_type text,
        schedule_agent text,
        last_sequence integer,
        last_dt_stamp timestamp with time zone,
        attendee_id integer,
        created timestamp with time zone DEFAULT (now()),
        modified timestamp with time zone DEFAULT (now()),
        CONSTRAINT pk_calendar_attendee PRIMARY KEY (id),
        CONSTRAINT fk_calendar_attendee_calendar_calendar_id FOREIGN KEY (calendar_id) REFERENCES calendar (id) ON DELETE CASCADE,
        CONSTRAINT fk_calendar_attendee_usr_attendee_id FOREIGN KEY (attendee_id) REFERENCES usr (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_address_collection_object_id ON address (collection_object_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_calendar_collection_object_id ON calendar (collection_object_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_calendar_organizer_id ON calendar (organizer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_calendar_attendee_attendee_id ON calendar_attendee (attendee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_calendar_attendee_calendar_id ON calendar_attendee (calendar_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_owner_id ON collection (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_parent_id ON collection (parent_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_principal_type_id ON collection (principal_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_group_member_id ON collection_group (member_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_object_actual_user_id ON collection_object (actual_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_object_collection_id ON collection_object (collection_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_object_owner_id ON collection_object (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_collection_property_modified_by_id ON collection_property (modified_by_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_grant_relation_grant_type_id ON grant_relation (grant_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_grant_relation_grantee_id ON grant_relation (grantee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_principal_type_label ON principal_type (label);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_push_subscription_resource_id ON push_subscription (resource_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_push_subscription_user_id ON push_subscription (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_sync_journal_collection_id ON sync_journal (collection_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_sync_journal_collection_object_id ON sync_journal (collection_object_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_usr_username ON usr (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_usr_credential_accesskey_credential_type_id ON usr_credential (accesskey, credential_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_usr_credential_credential_type_id ON usr_credential (credential_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE INDEX ix_usr_credential_usr_id ON usr_credential (usr_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    CREATE UNIQUE INDEX ix_usr_credential_type_label ON usr_credential_type (label);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20250425070016_CreateInitial') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20250425070016_CreateInitial', '9.0.8');
    END IF;
END $EF$;
COMMIT;

