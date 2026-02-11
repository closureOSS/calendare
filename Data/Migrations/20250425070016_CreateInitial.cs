using System;
using System.Collections;
using System.Collections.Generic;
using Calendare.Data.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Calendare.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:collection_sub_type", "calendar_proxy_read,calendar_proxy_write,default,scheduling_inbox,scheduling_outbox,web_push_subscription")
                .Annotation("Npgsql:Enum:collection_type", "addressbook,calendar,collection,principal");

            migrationBuilder.CreateSequence(
                name: "EntityFrameworkHiLoSequence",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "__data_migration_history",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_on = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk___data_migration_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_message",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    uid = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    sender_email = table.Column<string>(type: "text", nullable: false),
                    receiver_email = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    processed = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grant_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    confers = table.Column<string>(type: "text", nullable: false),
                    privileges = table.Column<BitArray>(type: "bit(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grant_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "principal_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_principal_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trx_journal",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    request_headers = table.Column<List<string>>(type: "text[]", nullable: false),
                    request_body = table.Column<string>(type: "text", nullable: true),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    response_error = table.Column<string>(type: "text", nullable: true),
                    response_headers = table.Column<List<string>>(type: "text[]", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trx_journal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usr",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    email_ok = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    date_format_type = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "text", nullable: true),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    closed = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usr", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usr_credential_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usr_credential_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "collection",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<int>(type: "integer", nullable: true),
                    uri = table.Column<string>(type: "text", nullable: false),
                    permanent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_type = table.Column<CollectionType>(type: "collection_type", nullable: false),
                    collection_sub_type = table.Column<CollectionSubType>(type: "collection_sub_type", nullable: false),
                    parent_container_uri = table.Column<string>(type: "text", nullable: true),
                    etag = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    order_by = table.Column<int>(type: "integer", nullable: true),
                    schedule_transparency = table.Column<string>(type: "text", nullable: true),
                    principal_type_id = table.Column<int>(type: "integer", nullable: true),
                    owner_prohibit = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    owner_mask = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    authorized_prohibit = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    authorized_mask = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    global_permit_self = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    global_permit = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection", x => x.id);
                    table.UniqueConstraint("ak_collection_uri", x => x.uri);
                    table.ForeignKey(
                        name: "fk_collection_collection_parent_id",
                        column: x => x.parent_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_principal_type_principal_type_id",
                        column: x => x.principal_type_id,
                        principalTable: "principal_type",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_collection_usr_owner_id",
                        column: x => x.owner_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usr_credential",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    usr_id = table.Column<int>(type: "integer", nullable: false),
                    credential_type_id = table.Column<int>(type: "integer", nullable: false),
                    accesskey = table.Column<string>(type: "text", nullable: false),
                    secret = table.Column<string>(type: "text", nullable: true),
                    locked = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    validity = table.Column<Interval>(type: "tstzrange", nullable: true),
                    last_used = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usr_credential", x => x.id);
                    table.ForeignKey(
                        name: "fk_usr_credential_usr_credential_type_credential_type_id",
                        column: x => x.credential_type_id,
                        principalTable: "usr_credential_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usr_credential_usr_usr_id",
                        column: x => x.usr_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_group",
                columns: table => new
                {
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    member_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_group", x => new { x.group_id, x.member_id });
                    table.ForeignKey(
                        name: "fk_collection_group_collection_group_id",
                        column: x => x.group_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_group_collection_member_id",
                        column: x => x.member_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_object",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    collection_id = table.Column<int>(type: "integer", nullable: false),
                    v_object_type = table.Column<string>(type: "text", nullable: false),
                    uri = table.Column<string>(type: "text", nullable: false),
                    uid = table.Column<string>(type: "text", nullable: false),
                    raw_data = table.Column<string>(type: "text", nullable: false),
                    etag = table.Column<string>(type: "text", nullable: false),
                    schedule_tag = table.Column<string>(type: "text", nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    is_private = table.Column<bool>(type: "boolean", nullable: false),
                    is_confidential = table.Column<bool>(type: "boolean", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    actual_user_id = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_object", x => x.id);
                    table.UniqueConstraint("ak_collection_object_uri", x => x.uri);
                    table.ForeignKey(
                        name: "fk_collection_object_collection_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_object_usr_actual_user_id",
                        column: x => x.actual_user_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_object_usr_owner_id",
                        column: x => x.owner_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_property",
                columns: table => new
                {
                    collection_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified_by_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_property", x => new { x.collection_id, x.name });
                    table.ForeignKey(
                        name: "fk_collection_property_collection_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_property_usr_modified_by_id",
                        column: x => x.modified_by_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_relation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    grantor_id = table.Column<int>(type: "integer", nullable: false),
                    grantee_id = table.Column<int>(type: "integer", nullable: false),
                    grant_type_id = table.Column<int>(type: "integer", nullable: false),
                    privileges = table.Column<BitArray>(type: "bit(16)", nullable: false),
                    is_indirect = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grant_relation", x => x.id);
                    table.UniqueConstraint("ak_grant_relation_grantor_id_grantee_id", x => new { x.grantor_id, x.grantee_id });
                    table.ForeignKey(
                        name: "fk_grant_relation_collection_grantee_id",
                        column: x => x.grantee_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_grant_relation_collection_grantor_id",
                        column: x => x.grantor_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_grant_relation_grant_type_grant_type_id",
                        column: x => x.grant_type_id,
                        principalTable: "grant_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_subscription",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    subscription_id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    push_destination_uri = table.Column<string>(type: "text", nullable: false),
                    client_public_key_type = table.Column<string>(type: "text", nullable: true),
                    client_public_key = table.Column<string>(type: "text", nullable: true),
                    auth_secret = table.Column<string>(type: "text", nullable: true),
                    content_encoding = table.Column<string>(type: "text", nullable: true),
                    fail_counter = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expiration = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    last_notification = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscription", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_subscription_collection_resource_id",
                        column: x => x.resource_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_push_subscription_usr_user_id",
                        column: x => x.user_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "address",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    collection_object_id = table.Column<int>(type: "integer", nullable: false),
                    formatted_name = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    nickname = table.Column<string>(type: "text", nullable: true),
                    birthday = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    card_version = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_address", x => x.id);
                    table.ForeignKey(
                        name: "fk_address_collection_object_collection_object_id",
                        column: x => x.collection_object_id,
                        principalTable: "collection_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    collection_object_id = table.Column<int>(type: "integer", nullable: false),
                    dtstart = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    dtend = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    due = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    completed = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    @class = table.Column<string>(name: "class", type: "text", nullable: true),
                    transp = table.Column<string>(type: "text", nullable: true),
                    rrule = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    percent_complete = table.Column<double>(type: "double precision", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    is_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    organizer_id = table.Column<int>(type: "integer", nullable: true),
                    dtstamp = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    last_modified = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    first_instance_start = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    first_instance_end = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    last_instance_start = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    last_instance_end = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_collection_object_collection_object_id",
                        column: x => x.collection_object_id,
                        principalTable: "collection_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_usr_organizer_id",
                        column: x => x.organizer_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sync_journal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<int>(type: "integer", nullable: false),
                    collection_object_id = table.Column<int>(type: "integer", nullable: true),
                    uri = table.Column<string>(type: "text", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    issued = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_journal", x => x.id);
                    table.ForeignKey(
                        name: "fk_sync_journal_collection_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sync_journal_collection_object_collection_object_id",
                        column: x => x.collection_object_id,
                        principalTable: "collection_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "calendar_attendee",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    calendar_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    rsvp = table.Column<bool>(type: "boolean", nullable: true),
                    participation_status = table.Column<string>(type: "text", nullable: true),
                    common_name = table.Column<string>(type: "text", nullable: true),
                    e_mail = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "text", nullable: true),
                    e_mail_state = table.Column<string>(type: "text", nullable: true),
                    attendee_state = table.Column<string>(type: "text", nullable: true),
                    attendee_type = table.Column<string>(type: "text", nullable: true),
                    schedule_agent = table.Column<string>(type: "text", nullable: true),
                    last_sequence = table.Column<int>(type: "integer", nullable: true),
                    last_dt_stamp = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    attendee_id = table.Column<int>(type: "integer", nullable: true),
                    created = table.Column<Instant>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    modified = table.Column<Instant>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_attendee", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_attendee_calendar_calendar_id",
                        column: x => x.calendar_id,
                        principalTable: "calendar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_attendee_usr_attendee_id",
                        column: x => x.attendee_id,
                        principalTable: "usr",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_address_collection_object_id",
                table: "address",
                column: "collection_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_collection_object_id",
                table: "calendar",
                column: "collection_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_organizer_id",
                table: "calendar",
                column: "organizer_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_attendee_attendee_id",
                table: "calendar_attendee",
                column: "attendee_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_attendee_calendar_id",
                table: "calendar_attendee",
                column: "calendar_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_owner_id",
                table: "collection",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_parent_id",
                table: "collection",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_principal_type_id",
                table: "collection",
                column: "principal_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_group_member_id",
                table: "collection_group",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_object_actual_user_id",
                table: "collection_object",
                column: "actual_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_object_collection_id",
                table: "collection_object",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_object_owner_id",
                table: "collection_object",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_property_modified_by_id",
                table: "collection_property",
                column: "modified_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_relation_grant_type_id",
                table: "grant_relation",
                column: "grant_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_relation_grantee_id",
                table: "grant_relation",
                column: "grantee_id");

            migrationBuilder.CreateIndex(
                name: "ix_principal_type_label",
                table: "principal_type",
                column: "label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscription_resource_id",
                table: "push_subscription",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscription_user_id",
                table: "push_subscription",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sync_journal_collection_id",
                table: "sync_journal",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_sync_journal_collection_object_id",
                table: "sync_journal",
                column: "collection_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usr_username",
                table: "usr",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usr_credential_accesskey_credential_type_id",
                table: "usr_credential",
                columns: new[] { "accesskey", "credential_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usr_credential_credential_type_id",
                table: "usr_credential",
                column: "credential_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_usr_credential_usr_id",
                table: "usr_credential",
                column: "usr_id");

            migrationBuilder.CreateIndex(
                name: "ix_usr_credential_type_label",
                table: "usr_credential_type",
                column: "label",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__data_migration_history");

            migrationBuilder.DropTable(
                name: "address");

            migrationBuilder.DropTable(
                name: "calendar_attendee");

            migrationBuilder.DropTable(
                name: "calendar_message");

            migrationBuilder.DropTable(
                name: "collection_group");

            migrationBuilder.DropTable(
                name: "collection_property");

            migrationBuilder.DropTable(
                name: "grant_relation");

            migrationBuilder.DropTable(
                name: "push_subscription");

            migrationBuilder.DropTable(
                name: "sync_journal");

            migrationBuilder.DropTable(
                name: "trx_journal");

            migrationBuilder.DropTable(
                name: "usr_credential");

            migrationBuilder.DropTable(
                name: "calendar");

            migrationBuilder.DropTable(
                name: "grant_type");

            migrationBuilder.DropTable(
                name: "usr_credential_type");

            migrationBuilder.DropTable(
                name: "collection_object");

            migrationBuilder.DropTable(
                name: "collection");

            migrationBuilder.DropTable(
                name: "principal_type");

            migrationBuilder.DropTable(
                name: "usr");

            migrationBuilder.DropSequence(
                name: "EntityFrameworkHiLoSequence");
        }
    }
}
