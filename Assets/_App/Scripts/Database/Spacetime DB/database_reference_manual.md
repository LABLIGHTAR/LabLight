# SpacetimeDB Database Reference Manual

This document provides an overview of the tables and reducers defined in the SpacetimeDB module.

## 1. Overview

### 1.1. General Introduction

The SpacetimeDB module is organized into several Rust files within the `server/src/` directory to improve modularity and maintainability. Each file typically groups related tables and their corresponding reducers. This manual details these components.

### 1.2. Authentication Model

This module uses an **OIDC-based authentication model**. User sign-up and sign-in are handled by an external OpenID Connect provider (e.g., Firebase Authentication, Auth0). The client application is responsible for:

1.  Authenticating the user with the OIDC provider.
2.  Obtaining an OIDC ID token.
3.  Passing this token to SpacetimeDB when connecting.

SpacetimeDB verifies the token and derives a unique, persistent `Identity` for the user. This `Identity` is used as the primary key in the `UserProfile` table and for authorization checks within reducers (`ctx.sender`).

New users must call the `register_profile` reducer after their first connection to create their associated `UserProfile` record.

### 1.3. Code Organization Summary

The module is organized as follows:

- `lib.rs`: Main module file (shared functions, `init`, client handlers, helper functions).
- `media_metadata.rs`: Media file metadata, uploads, cleanup (`UploadStatus`, `MediaMetadata`, `MediaPendingUploadCleanupTimer` tables and reducers).
- `user_profile.rs`: User profiles (`UserProfile` table and reducers).
- `client_device.rs`: Client connection status (`ClientDevice` table).
- `organization.rs`: Organizations, memberships, join requests (`Organization`, `OrganizationMember`, `OrganizationMemberRequest` tables and reducers).
- `protocol.rs`: Protocol definitions, ownership, edit history, saved protocols (`Protocol`, `ProtocolOwnership`, `ProtocolEditHistory`, `SavedProtocol` tables and reducers).
- `protocol_state.rs`: Protocol instance states, ownership, edit history, cleanup (`ProtocolState`, `ProtocolStateOwnership`, `ProtocolStateEditHistory`, `ProtocolStateCleanupTimer` tables and reducers).
- `organization_notice.rs`: Temporary organization notices and cleanup (`OrganizationNotice`, `OrganizationNoticeCleanupTimer` tables and reducers).
- `scheduled_task.rs`: Scheduled protocol tasks, assignments, status (`ScheduledTaskStatus` enum, `ScheduledProtocolTask`, `ScheduledTaskAssignee`, `ScheduledTaskOverdueTimer` tables and reducers).
- `messaging.rs`: Real-time user-to-user and group messaging (`Conversation`, `ConversationParticipant`, `Message` tables and reducers).

## 2. Database Tables

### 2.1. Core & User Management

(Primarily from `client_device.rs`, `user_profile.rs`)

#### 2.1.1. `client_device` (public)

(Located in `server/src/client_device.rs`)

Represents a connected client's connection status. The primary key `identity` corresponds to the authenticated user's OIDC-derived `Identity`.

**Struct:** `ClientDevice`
**Fields:**

- `identity`: `Identity` (`#[primary_key]`) - Unique identifier for the user's connection. Links 1-to-1 with `UserProfile.identity`.
- `connected`: `bool` - Whether the client is currently connected.
- `created_at`: `Timestamp` - When this record was first created.
- `last_connected`: `Timestamp` - Timestamp of the last connection.
- `last_disconnected`: `Timestamp` - Timestamp of the last disconnection.

#### 2.1.2. `user_profile` (public)

(Located in `server/src/user_profile.rs`)

Represents a user's profile information.

**Struct:** `UserProfile`
**Fields:**

- `identity`: `Identity` (`#[primary_key]`) - User's OIDC-derived identity. Foreign key in many tables.
- `name`: `String` - Display name.
- `online`: `bool` - Whether user is currently online.
- `created_at`: `Timestamp` - Profile creation time.
- `last_online`: `Timestamp` - Last time user was marked online.

### 2.2. Organization Management

(Primarily from `organization.rs`, `organization_notice.rs`)

#### 2.2.1. `organization` (public)

(Located in `server/src/organization.rs`)

Represents an organization or group.

**Struct:** `Organization`
**Fields:**

- `id`: `u32` (`#[primary_key]`, `#[auto_inc]`) - Unique ID. Foreign key in related tables.
- `name`: `String` (`#[unique]`) - Unique organization name.
- `created_at`: `Timestamp` - Creation time.
- `owner_identity`: `Identity` (`#[unique]`, `#[index(btree)]`) - Owning user's `Identity`. References `UserProfile.identity`.
- `owner_display_name`: `String` - **Denormalized** owner's display name.

#### 2.2.2. `organization_member` (public)

(Located in `server/src/organization.rs`)

Represents a user's membership in an organization.

**Struct:** `OrganizationMember`
**Fields:**

- `id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique internal ID for the membership record. (Field is private in struct, but `#[primary_key]` applies).
- `organization_id`: `u32` (`#[index(btree)]`) - References `Organization.id`.
- `member_identity`: `Identity` (`#[index(btree)]`) - Member's `UserProfile.identity`.

#### 2.2.3. `organization_member_request` (public)

(Located in `server/src/organization.rs`)

Represents a pending request to join an organization.

**Struct:** `OrganizationMemberRequest`
**Fields:**

- `id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `organization_id`: `u32` (`#[index(btree)]`) - References `Organization.id`.
- `requester_identity`: `Identity` (`#[index(btree)]`) - Requester's `UserProfile.identity`.
- `created_at`: `Timestamp` - Request creation time.

#### 2.2.4. `organization_notice` (public)

(Located in `server/src/organization_notice.rs`)
Represents a temporary notice in an organization.

**Struct:** `OrganizationNotice`
**Fields:**

- `notice_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `organization_id`: `u32` (`#[index(btree)]`) - References `Organization.id`.
- `poster_identity`: `Identity` (`#[index(btree)]`) - Poster's `UserProfile.identity`.
- `content`: `String` - Notice content.
- `created_at`: `Timestamp` - Posting time.
- `duration_seconds`: `u64` - Visibility duration.
- `expires_at`: `Timestamp` (`#[index(btree)]`) - Expiry time.

#### 2.2.5. `organization_notice_cleanup_timer`

(Located in `server/src/organization_notice.rs`)
Schedules cleanup of expired organization notices.

**Struct:** `OrganizationNoticeCleanupTimer`
**Fields:**

- `timer_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the timer schedule entry.
- `scheduled_at`: `spacetimedb::ScheduleAt` - Defines the scheduling parameters (interval).
  **Scheduled Reducer:** `run_organization_notice_cleanup`

### 2.3. Protocol Definition Management

(Primarily from `protocol.rs`)

#### 2.3.1. `protocol` (public)

(Located in `server/src/protocol.rs`)

Represents a protocol definition.

**Struct:** `Protocol`
**Fields:**

- `id`: `u32` (`#[primary_key]`, `#[auto_inc]`) - Unique ID. Foreign key in related tables.
- `name`: `String` - Protocol name.
- `content`: `String` - Protocol content (e.g., JSON, XML).
- `created_at`: `Timestamp` - Creation time.
- `edited_at`: `Timestamp` - Last edit time.
- `version`: `u64` - Version number.
- `is_public`: `bool` - Public accessibility.

#### 2.3.2. `protocol_ownership` (public)

(Located in `server/src/protocol.rs`)

Defines protocol ownership by users or organizations.

**Struct:** `ProtocolOwnership`
**Fields:**

- `protocol_id`: `u32` (`#[primary_key]`, `#[index(btree)]`) - References `Protocol.id`.
- `owner_identity`: `Identity` (`#[index(btree)]`) - Owning `UserProfile.identity` (or contact user if org-owned).
- `organization_id`: `u32` - References `Organization.id` (if > 0, org ownership).
- `owner_display_name`: `String` - **Denormalized** owner/organization name.

#### 2.3.3. `protocol_edit_history` (public)

(Located in `server/src/protocol.rs`)

Tracks protocol edit history for rollbacks.

**Struct:** `ProtocolEditHistory`
**Fields:**

- `edit_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `protocol_id`: `u32` (`#[index(btree)]`) - References `Protocol.id`.
- `editor_identity`: `Identity` (`#[index(btree)]`) - Editor's `UserProfile.identity`.
- `edited_at`: `Timestamp` - Edit time.
- `version`: `u64` - Protocol version _after_ this edit was applied.
- `previous_content`: `String` - Protocol content _before_ this edit was applied.

#### 2.3.4. `saved_protocol` (public)

(Located in `server/src/protocol.rs`)

Links a user to a "saved" protocol.

**Struct:** `SavedProtocol`
**Fields:**

- `id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `user_identity`: `Identity` (`#[index(btree)]`) - User's `UserProfile.identity`.
- `protocol_id`: `u32` (`#[index(btree)]`) - References `Protocol.id`.
- `saved_at`: `Timestamp` - Save time.

### 2.4. Protocol State Management

(Primarily from `protocol_state.rs`)

#### 2.4.1. `protocol_state` (public)

(Located in `server/src/protocol_state.rs`)

Represents state/instance data of a protocol run.

**Struct:** `ProtocolState`
**Fields:**

- `id`: `u32` (`#[primary_key]`, `#[auto_inc]`) - Unique ID. Foreign key in related tables.
- `protocol_id`: `u32` (`#[index(btree)]`) - References `Protocol.id`.
- `creator_identity`: `Identity` (`#[index(btree)]`) - Creator's `UserProfile.identity`.
- `state`: `String` - State data (e.g., JSON).
- `created_at`: `Timestamp` - Creation time.
- `edited_at`: `Timestamp` (`#[index(btree)]`) - Last edit time.

#### 2.4.2. `protocol_state_ownership` (public)

(Located in `server/src/protocol_state.rs`)

Defines ownership of a `ProtocolState`.

**Struct:** `ProtocolStateOwnership`
**Fields:**

- `protocol_state_id`: `u32` (`#[primary_key]`) - References `ProtocolState.id` (1-to-1).
- `owner_identity`: `Identity` (`#[index(btree)]`) - Owning `UserProfile.identity` (or contact user if org-owned).
- `organization_id`: `u32` (`#[index(btree)]`) - Owning `Organization.id` (if > 0).
- `owner_display_name`: `String` - **Denormalized** owner/organization name.

#### 2.4.3. `protocol_state_edit_history` (public)

(Located in `server/src/protocol_state.rs`)

Tracks edits to a `ProtocolState`.

**Struct:** `ProtocolStateEditHistory`
**Fields:**

- `edit_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `protocol_state_id`: `u32` (`#[index(btree)]`) - References `ProtocolState.id`.
- `editor_identity`: `Identity` (`#[index(btree)]`) - Editor's `UserProfile.identity`.
- `edited_at`: `Timestamp` - Edit time.

#### 2.4.4. `protocol_state_cleanup_timer`

(Located in `server/src/protocol_state.rs`)

Schedules periodic cleanup of old protocol states.

**Struct:** `ProtocolStateCleanupTimer`
**Fields:**

- `id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the schedule entry (actually `pub id: u64` in `lib.rs` init, but `id: 0` is used, and struct has `pub id: u64`).
- `scheduled_at`: `spacetimedb::ScheduleAt` - Defines the scheduling parameters (interval).
  **Scheduled Reducer:** `cleanup_old_protocol_states`

### 2.5. Scheduled Task Management

(Primarily from `scheduled_task.rs`)

#### 2.5.0. Enum: `ScheduledTaskStatus`

(Located in `server/src/scheduled_task.rs`)

Represents the current status of a `ScheduledProtocolTask`.

**Variants:**

- `Pending`: The task is scheduled but not yet started.
- `InProgress`: An assignee has started working on the task.
- `Completed`: An assignee has marked the task as complete.
- `Overdue`: The task's `due_at` timestamp has passed, and it was not completed.
- `Cancelled`: The assigner has cancelled the task.

#### 2.5.1. `scheduled_protocol_task` (public)

(Located in `server/src/scheduled_task.rs`)

Represents a scheduled task for a protocol part.

**Struct:** `ScheduledProtocolTask`
**Fields:**

- `task_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID. Foreign key in `ScheduledTaskAssignee`.
- `organization_id`: `u32` (`#[index(btree)]`) - References `Organization.id`.
- `protocol_id`: `u32` (`#[index(btree)]`) - References `Protocol.id`.
- `protocol_state_id`: `u32` (`#[index(btree)]`) - References `ProtocolState.id`.
- `assigner_identity`: `Identity` (`#[index(btree)]`) - Assigner's `UserProfile.identity`.
- `start_step`: `u32` - Start step in protocol.
- `end_step`: `u32` - End step in protocol.
- `scheduled_at`: `Timestamp` - Activation time.
- `due_at`: `Timestamp` (`#[index(btree)]`) - Deadline.
- `created_at`: `Timestamp` - Creation time.
- `status`: `ScheduledTaskStatus` - Current task status.
- `completed_at`: `Option<Timestamp>` - Completion time.

#### 2.5.2. `scheduled_task_assignee` (public)

(Located in `server/src/scheduled_task.rs`)

Links a `ScheduledProtocolTask` to an assigned user.

**Struct:** `ScheduledTaskAssignee`
**Fields:**

- `assignment_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `task_id`: `u64` (`#[index(btree)]`) - References `ScheduledProtocolTask.task_id`.
- `assignee_identity`: `Identity` (`#[index(btree)]`) - Assignee's `UserProfile.identity`.

#### 2.5.3. `scheduled_task_overdue_timer`

(Located in `server/src/scheduled_task.rs`)

Schedules periodic check for overdue tasks.

**Struct:** `ScheduledTaskOverdueTimer`
**Fields:**

- `timer_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the timer schedule entry.
- `scheduled_at`: `spacetimedb::ScheduleAt` - Defines the scheduling parameters (interval).
  **Scheduled Reducer:** `check_overdue_protocol_tasks`

### 2.6. Media Management

(Primarily from `media_metadata.rs`)

#### 2.6.0. Enum: `UploadStatus`

(Located in `server/src/media_metadata.rs`)

Represents the status of a media file upload.

**Variants:**

- `PendingUpload`: An upload slot has been requested, and the system is awaiting the actual file upload and confirmation.
- `Available`: The file upload has been successfully confirmed, and the media is ready for use.
- `Failed`: The file upload attempt failed or was cancelled.

#### 2.6.1. `MediaMetadata` (public)

(Located in `server/src/media_metadata.rs`)

Stores metadata for uploaded media files.

**Struct:** `MediaMetadata`
**Fields:**

- `media_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID.
- `object_key`: `String` (`#[unique]`) - Client-generated unique key for object storage.
- `owner_identity`: `Identity` - Uploader's `UserProfile.identity`.
- `original_filename`: `String` - Original file name.
- `content_type`: `String` - MIME type.
- `file_size`: `Option<u64>` - File size in bytes (post-confirmation).
- `upload_status`: `UploadStatus` - Upload status.
- `created_at`: `Timestamp` - Request time for upload slot.
- `upload_completed_at`: `Option<Timestamp>` - Upload confirmation time.

#### 2.6.2. `MediaPendingUploadCleanupTimer`

(Located in `server/src/media_metadata.rs`)

Schedules cleanup of stale pending media uploads.

**Struct:** `MediaPendingUploadCleanupTimer`
**Fields:**

- `timer_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the timer schedule entry.
- `scheduled_at`: `spacetimedb::ScheduleAt` - Defines the scheduling parameters (interval).
  **Scheduled Reducer:** `cleanup_stale_media_pending_uploads`

### 2.7. Messaging

(Primarily from `server/src/messaging.rs`)

This section details the tables that form the core of the real-time messaging system.

#### 2.7.0. Enum: `ConversationStatus`

Represents the status of a conversation.

**Variants:**

- `Active`: The conversation is ongoing.
- `Archived`: The conversation is archived, typically after all participants have left.

#### 2.7.1. Enum: `MessageType`

Distinguishes between user-generated messages and system-generated notifications within a conversation.

**Variants:**

- `User`: A standard message sent by a user.
- `System`: An automated message indicating an event (e.g., user joined/left, conversation renamed).

#### 2.7.2. `Conversation` (public)

Represents a single conversation thread.

**Struct:** `Conversation`
**Fields:**

- `conversation_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the conversation.
- `last_message_at`: `Timestamp` - Timestamp of the most recent message, used for sorting conversation lists.
- `participants_hash`: `String` (`#[unique]`) - A deterministic hash of the participants' `Identity` values. Used to uniquely identify direct message conversations between a specific set of users.
- `name`: `Option<String>` - An optional, user-defined name for the conversation.
- `created_at`: `Timestamp` - When the conversation was created.
- `created_by`: `Identity` - The `Identity` of the user who initiated the conversation.
- `status`: `ConversationStatus` - The current status of the conversation (e.g., `Active`).

#### 2.7.3. `ConversationParticipant` (public)

Links a user to a conversation, tracking their membership and status within it.

**Struct:** `ConversationParticipant`
**Fields:**

- `participant_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the participation record.
- `conversation_id`: `u64` (`#[index(btree)]`) - References `Conversation.conversation_id`.
- `participant_identity`: `Identity` (`#[index(btree)]`) - The participant's `UserProfile.identity`.
- `joined_at`: `Timestamp` - When the user joined the conversation.
- `last_viewed_at`: `Timestamp` - The timestamp of the last time the user viewed the conversation, used for unread message tracking.

#### 2.7.4. `Message` (public)

Represents a single message or system event within a conversation.

**Struct:** `Message`
**Fields:**

- `message_id`: `u64` (`#[primary_key]`, `#[auto_inc]`) - Unique ID for the message.
- `conversation_id`: `u64` (`#[index(btree)]`) - References `Conversation.conversation_id`.
- `sender_identity`: `Identity` - The `Identity` of the message sender.
- `content`: `String` - The text content of the message.
- `last_edited_at`: `Option<Timestamp>` - Timestamp of the last edit, if any.
- `message_type`: `MessageType` - The type of message (`User` or `System`).
- `is_deleted`: `bool` - A soft-delete flag. If true, the message is hidden from view but remains in the database.
- `sent_at`: `Timestamp` - When the message was sent.

---

## 3. Reducers

### 3.1. Lifecycle & Core (`lib.rs`)

#### 3.1.1. `init` (Lifecycle)

(Located in `server/src/lib.rs`)

- **Function:** `init(ctx: &ReducerContext)`
- **Return Type:** `Result<(), String>`
- **Description:** Initializes the module when first published or cleared. Ensures `ProtocolStateCleanupTimer`, `OrganizationNoticeCleanupTimer`, `ScheduledTaskOverdueTimer`, and `MediaPendingUploadCleanupTimer` are scheduled if they don't already exist.

#### 3.1.2. `client_connected` (Lifecycle)

(Located in `server/src/lib.rs`)

- **Function:** `client_connected(ctx: &ReducerContext)`
- **Return Type:** `()`
- **Description:** Handles new client connections. Creates or updates a `ClientDevice` record (sets `connected = true`, `last_connected`). Updates the associated `UserProfile` (if exists) to set `online = true` and `last_online`.

#### 3.1.3. `client_disconnected` (Lifecycle)

(Located in `server/src/lib.rs`)

- **Function:** `client_disconnected(ctx: &ReducerContext)`
- **Return Type:** `()`
- **Description:** Handles client disconnections. Updates `ClientDevice` record (sets `connected = false`, `last_disconnected`). Updates the associated `UserProfile` (if exists) to set `online = false` and `last_online`.

### 3.2. User Profile Management

(Primarily from `server/src/user_profile.rs`)

#### 3.2.1. `register_profile`

(Located in `server/src/user_profile.rs`)

- **Function:** `register_profile(ctx: &ReducerContext, name: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Creates a `UserProfile` record for the calling user's `Identity` (`ctx.sender`). Fails if a profile already exists or if the name is empty. Sets initial `online` status to true.

#### 3.2.2. `update_user_profile_name`

(Located in `server/src/user_profile.rs`)

- **Function:** `update_user_profile_name(ctx: &ReducerContext, new_name: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the calling user (`ctx.sender`) to update their display name in `UserProfile`. Fails if new name is empty. Also iterates through `Organization` (owned by user), `ProtocolOwnership` (user-owned only), and `ProtocolStateOwnership` (user-owned only) to update the denormalized `owner_display_name` field.

### 3.3. Organization Management

(Primarily from `server/src/organization.rs`, `server/src/organization_notice.rs`)

#### 3.3.1. `try_create_organization`

(Located in `server/src/organization.rs`)

- **Function:** `try_create_organization(ctx: &ReducerContext, name: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Creates a new organization. Requires the caller (`ctx.sender`) to have a registered `UserProfile`. Checks if the organization name is unique. Inserts a new record into `Organization`, setting the owner to the caller's `Identity` and `owner_display_name` to the caller's profile name.

#### 3.3.2. `update_organization_name`

(Located in `server/src/organization.rs`)

- **Function:** `update_organization_name(ctx: &ReducerContext, organization_id: u32, new_name: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the owner of an organization (`ctx.sender`) to update its name in the `Organization` table. Checks that the new name is unique among other organizations and not empty. Also iterates through `ProtocolOwnership` and `ProtocolStateOwnership` to update the denormalized `owner_display_name` field where the organization is the owner.

#### 3.3.3. `request_join_organization`

(Located in `server/src/organization.rs`)

- **Function:** `request_join_organization(ctx: &ReducerContext, organization_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user with a registered `UserProfile` (`ctx.sender`) to request membership in an organization. Checks if the organization exists, if the user is the owner, already a member, or if a request is already pending. Inserts a record into `OrganizationMemberRequest`.

#### 3.3.4. `try_approve_organization_member_request`

(Located in `server/src/organization.rs`)

- **Function:** `try_approve_organization_member_request(ctx: &ReducerContext, request_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the owner of an organization (`ctx.sender`) to approve a pending membership request identified by `request_id`.
- **Parameters**: `request_id: u64` - The ID of the `OrganizationMemberRequest` to approve.
- **Permissions**: Caller must be the owner of the organization associated with the request. Caller must have a registered `UserProfile`.
- **Action**: Fetches the `OrganizationMemberRequest`. Verifies permissions. Deletes the request from `OrganizationMemberRequest` and inserts a new record into `OrganizationMember` for the `requester_identity` if not already a member.

#### 3.3.5. `try_deny_organization_member_request`

(Located in `server/src/organization.rs`)

- **Function:** `try_deny_organization_member_request(ctx: &ReducerContext, request_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the owner of an organization (`ctx.sender`) to deny a pending membership request.
- **Parameters**: `request_id: u64` - The ID of the `OrganizationMemberRequest` to deny.
- **Permissions**: Caller must be the owner of the organization associated with the request. Caller must have a registered `UserProfile`.
- **Action**: Fetches the `OrganizationMemberRequest`. Verifies permissions. Deletes the request from `OrganizationMemberRequest`.

#### 3.3.6. `try_leave_organization`

(Located in `server/src/organization.rs`)

- **Function:** `try_leave_organization(ctx: &ReducerContext, organization_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user (`ctx.sender`) who is a member of an organization (but not the owner) to leave it.
- **Parameters**: `organization_id: u32` - The ID of the organization to leave.
- **Permissions**: Caller must have a registered `UserProfile`. Caller must be a member but not the owner of the organization.
- **Action**: Deletes the `OrganizationMember` record for the user and organization.

#### 3.3.7. `try_delete_organization`

(Located in `server/src/organization.rs`)

- **Function:** `try_delete_organization(ctx: &ReducerContext, organization_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the owner of an organization (`ctx.sender`) to delete it and all associated data.
- **Parameters**: `organization_id: u32` - The ID of the organization to delete.
- **Permissions**: Caller must be the owner of the organization. Caller must have a registered `UserProfile`.
- **Action**: Deletes the `Organization` record. Also deletes associated `OrganizationMember` records, `OrganizationMemberRequest` records, `OrganizationNotice` records, `ScheduledProtocolTask` and `ScheduledTaskAssignee` records, `ProtocolOwnership` records (where org is owner), and `ProtocolStateOwnership` records (where org is owner, along with their `ProtocolState` and `ProtocolStateEditHistory`).

#### 3.3.8. `try_post_organization_notice`

(Located in `server/src/organization_notice.rs`)

- **Function:** `try_post_organization_notice(ctx: &ReducerContext, organization_id: u32, content: String, duration_seconds: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a member or owner of an organization (`ctx.sender`) to post a temporary notice. Requires the caller to have a registered `UserProfile`. Content cannot be empty, duration must be positive. Calculates `expires_at`. Inserts a new record into `OrganizationNotice`.

#### 3.3.9. `run_organization_notice_cleanup` (Scheduled)

(Located in `server/src/organization_notice.rs`)

- **Function:** `run_organization_notice_cleanup(ctx: &ReducerContext, _timer: OrganizationNoticeCleanupTimer)`
- **Return Type:** `Result<(), String>`
- **Description:** Scheduled reducer triggered periodically by `OrganizationNoticeCleanupTimer`. Verifies it's called by the scheduler, iterates through `OrganizationNotice` records, and deletes those where `expires_at` is less than or equal to `ctx.timestamp`.

#### 3.3.10. `try_delete_organization_notice`

(Located in `server/src/organization_notice.rs`)

- **Function:** `try_delete_organization_notice(ctx: &ReducerContext, notice_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the user who posted an `OrganizationNotice` (`poster_identity`) or the owner of the organization to delete the notice manually. Requires `ctx.sender` to have `UserProfile`. Finds notice by `notice_id` and deletes if authorized.

### 3.4. Protocol Definition Management

(Primarily from `server/src/protocol.rs`)

#### 3.4.1. `try_create_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_create_protocol(ctx: &ReducerContext, name: String, content: String, is_public: bool, organization_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Creates a new protocol. Requires caller (`ctx.sender`) to have `UserProfile`.
  - If `organization_id` is `0`, protocol is user-owned. `owner_identity` in `ProtocolOwnership` is `ctx.sender`, `owner_display_name` is user's profile name.
  - If `organization_id` is `> 0`, protocol is org-owned. Caller must be member/owner of org. `owner_identity` in `ProtocolOwnership` is `ctx.sender` (as contact), `owner_display_name` is org's name.
- **Name Uniqueness**: Protocol `name` must be unique for the owning entity (user or org).
- **Action**: Inserts into `Protocol`, `ProtocolOwnership`, and initial `ProtocolEditHistory` (with empty `previous_content`).

#### 3.4.2. `try_edit_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_edit_protocol(ctx: &ReducerContext, protocol_id: u32, new_name: String, content: String, is_public: bool, new_owner_organization_id: Option<u32>)`
- **Return Type:** `Result<(), String>`
- **Description:** Edits protocol name, content, public status, and optionally ownership. Requires `ctx.sender` to have `UserProfile`. Name cannot be empty.
- **Permissions**:
  - Current Edit: Caller must be user-owner or member/owner of current owning org.
  - Ownership Transfer: If transferring to new org, caller must be member/owner of target org.
- **Name Uniqueness**: `new_name` is unique under target owner (user or org). Suffix `_N` appended if conflict.
- **Action**: Updates `Protocol` (name, content, is_public, edited_at, version++). If ownership changes, updates `ProtocolOwnership` (org_id, owner_identity to `ctx.sender`, owner_display_name). Inserts `ProtocolEditHistory` with previous content.

#### 3.4.3. `try_rollback_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_rollback_protocol(ctx: &ReducerContext, protocol_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Rolls back a protocol to its previous version. Requires `ctx.sender` to have `UserProfile` and be user-owner or member/owner of owning org. Finds latest `ProtocolEditHistory`, restores `previous_content` to `Protocol`, decrements `version`, updates `edited_at`, deletes used history entry. Cannot rollback version 1.

#### 3.4.4. `try_delete_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_delete_protocol(ctx: &ReducerContext, protocol_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Deletes a protocol and all associated data. Requires caller (`ctx.sender`) to be user-owner or member/owner of owning org.
- **Action**: Deletes `ProtocolOwnership`, associated `ProtocolState` (and their `ProtocolStateOwnership`, `ProtocolStateEditHistory`), `ProtocolEditHistory` for the protocol, and `SavedProtocol` entries, then the `Protocol` itself.

#### 3.4.5. `try_fork_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_fork_protocol(ctx: &ReducerContext, original_protocol_id: u32, new_name: String, new_is_public: bool)`
- **Parameters**: `original_protocol_id: u32`, `new_name: String`, `new_is_public: bool`.
- **Return Type:** `Result<(), String>`
- **Description:** Forks an existing protocol, creating a new, user-owned copy (`organization_id = 0`) for the caller (`ctx.sender`).
- **Permissions**: Caller must have `UserProfile`. Caller must have access to original protocol (public, or user-owner, or member/owner of owning org). `new_name` must be unique among caller's user-owned protocols.
- **Action**: Creates new `Protocol` (version 1, content copied), `ProtocolOwnership` (user-owned by caller), and initial `ProtocolEditHistory`.

#### 3.4.6. `try_save_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_save_protocol(ctx: &ReducerContext, protocol_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user (`ctx.sender`) to "save" a protocol for easy access.
- **Permissions**: Caller must have `UserProfile`. Caller must have access to the protocol (public, or user-owner, or member/owner of owning org). Fails if already saved by the user.
- **Action**: Inserts a new record into `SavedProtocol`.

#### 3.4.7. `try_unsave_protocol`

(Located in `server/src/protocol.rs`)

- **Function:** `try_unsave_protocol(ctx: &ReducerContext, protocol_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user (`ctx.sender`) to "unsave" a protocol they previously saved.
- **Permissions**: Caller must have `UserProfile`.
- **Action**: Deletes the corresponding record from `SavedProtocol`.

### 3.5. Protocol State Management

(Primarily from `server/src/protocol_state.rs`)

#### 3.5.1. `try_create_protocol_state`

(Located in `server/src/protocol_state.rs`)

- **Function:** `try_create_protocol_state(ctx: &ReducerContext, protocol_id: u32, organization_id: u32, state: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Creates a new state record for a protocol. Requires `ctx.sender` to have `UserProfile`. Protocol must exist.
  - If `organization_id` is 0, state is user-owned. `owner_identity` in `ProtocolStateOwnership` is `ctx.sender`.
  - If `organization_id` > 0, state is org-owned. Caller must be member/owner of org. `owner_identity` is `ctx.sender` (as contact).
- **Action**: Inserts into `ProtocolState`, `ProtocolStateOwnership` (with `owner_display_name` from user/org), and initial `ProtocolStateEditHistory`.

#### 3.5.2. `try_delete_protocol_state`

(Located in `server/src/protocol_state.rs`)

- **Function:** `try_delete_protocol_state(ctx: &ReducerContext, protocol_state_id: u32)`
- **Return Type:** `Result<(), String>`
- **Description:** Deletes a protocol state and its associated data.
- **Permissions**: Caller (`ctx.sender`) must have `UserProfile`. Caller must be user-owner of the state or member/owner of the owning organization.
- **Action**: Deletes all `ProtocolStateEditHistory` for the state, then `ProtocolStateOwnership`, then the `ProtocolState` itself.

#### 3.5.3. `try_edit_protocol_state`

(Located in `server/src/protocol_state.rs`)

- **Function:** `try_edit_protocol_state(ctx: &ReducerContext, protocol_state_id: u32, new_state: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Edits the `state` string of an existing `ProtocolState`.
- **Permissions**: Caller (`ctx.sender`) must have `UserProfile`. Caller must be user-owner of the state or member/owner of the owning organization.
- **Action**: Updates `state` and `edited_at` in `ProtocolState`. Inserts a new record into `ProtocolStateEditHistory`.

#### 3.5.4. `cleanup_old_protocol_states` (Scheduled)

(Located in `server/src/protocol_state.rs`, triggered by `ProtocolStateCleanupTimer`)

- **Function:** `cleanup_old_protocol_states(ctx: &ReducerContext, _schedule_args: ProtocolStateCleanupTimer)`
- **Return Type:** `Result<(), String>`
- **Description:** Periodically cleans up `ProtocolState` records older than 60 days (based on `edited_at`). Must be called by scheduler. Deletes associated `ProtocolStateEditHistory`, `ProtocolStateOwnership`, and then the `ProtocolState`.

### 3.6. Scheduled Task Management

(Primarily from `server/src/scheduled_task.rs`)

#### 3.6.1. `try_schedule_protocol_task`

(Located in `server/src/scheduled_task.rs`)

- **Function:** `try_schedule_protocol_task(ctx: &ReducerContext, organization_id: u32, protocol_id: u32, protocol_state_id: u32, assignee_identities: Vec<Identity>, start_step: u32, end_step: u32, scheduled_at: Timestamp, due_at: Timestamp)`
- **Return Type:** `Result<(), String>`
- **Description:** Schedules a task for a part of a protocol for organization members.
- **Permissions**: Assigner (`ctx.sender`) must have `UserProfile` and be member/owner of `organization_id`. Assignees must have `UserProfile` and be members/owners of `organization_id`. Protocol, Organization, and ProtocolState must exist and be correctly associated.
- **Validations**: At least one assignee. `start_step <= end_step`. `scheduled_at <= due_at`. `protocol_state_id` must belong to `protocol_id` and `organization_id`.
- **Action**: Creates `ScheduledProtocolTask` (status `Pending`). Creates `ScheduledTaskAssignee` records for each assignee.

#### 3.6.2. `try_start_scheduled_task`

(Located in `server/src/scheduled_task.rs`)

- **Function:** `try_start_scheduled_task(ctx: &ReducerContext, task_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows an assigned user (`ctx.sender`) to mark a task as `InProgress`.
- **Permissions**: Caller must have `UserProfile` and be an assignee of the task.
- **Conditions**: Task status must be `Pending`.
- **Action**: Updates `ScheduledProtocolTask.status` to `InProgress`.

#### 3.6.3. `try_complete_scheduled_task`

(Located in `server/src/scheduled_task.rs`)

- **Function:** `try_complete_scheduled_task(ctx: &ReducerContext, task_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows an assigned user (`ctx.sender`) to mark a task as `Completed`.
- **Permissions**: Caller must have `UserProfile` and be an assignee of the task.
- **Conditions**: Task status must be `Pending`, `InProgress`, or `Overdue`. Cannot complete `Cancelled` or already `Completed` tasks.
- **Action**: Updates `ScheduledProtocolTask.status` to `Completed` and sets `completed_at`.

#### 3.6.4. `try_cancel_scheduled_task`

(Located in `server/src/scheduled_task.rs`)

- **Function:** `try_cancel_scheduled_task(ctx: &ReducerContext, task_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the original assigner (`ctx.sender`) of a task to cancel it.
- **Permissions**: Caller must have `UserProfile` and be the `assigner_identity` of the task.
- **Conditions**: Task status must be `Pending`, `InProgress`, or `Overdue`. Cannot cancel `Completed` or already `Cancelled` tasks.
- **Action**: Updates `ScheduledProtocolTask.status` to `Cancelled` and clears `completed_at`.

#### 3.6.5. `check_overdue_protocol_tasks` (Scheduled)

(Located in `server/src/scheduled_task.rs`, triggered by `ScheduledTaskOverdueTimer`)

- **Function:** `check_overdue_protocol_tasks(ctx: &ReducerContext, _timer: ScheduledTaskOverdueTimer)`
- **Return Type:** `Result<(), String>`
- **Description:** Periodically checks for `ScheduledProtocolTask` entries that are `Pending` and past their `due_at` time. Updates their status to `Overdue`. Must be called by scheduler.

### 3.7. Media Management

(Primarily from `server/src/media_metadata.rs`)

#### 3.7.1. `request_media_upload_slot`

(Located in `server/src/media_metadata.rs`)

- **Function:** `request_media_upload_slot(ctx: &ReducerContext, object_key: String, original_filename: String, content_type: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows client (`ctx.sender`) to request an upload slot. Client provides unique `object_key`. Creates `MediaMetadata` record with `upload_status = PendingUpload`.
- **Permissions**: Caller must have `UserProfile`. `object_key` must be unique.

#### 3.7.2. `confirm_media_upload_complete`

(Located in `server/src/media_metadata.rs`)

- **Function:** `confirm_media_upload_complete(ctx: &ReducerContext, object_key: String, file_size: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Client (`ctx.sender`) confirms media upload for `object_key`. Updates `MediaMetadata`.
- **Permissions**: Caller must have `UserProfile` and be `owner_identity` of the `MediaMetadata` entry. Entry status must be `PendingUpload`.
- **Action**: Updates `upload_status` to `Available`, sets `file_size` and `upload_completed_at`.

#### 3.7.3. `delete_media_metadata`

(Located in `server/src/media_metadata.rs`)

- **Function:** `delete_media_metadata(ctx: &ReducerContext, object_key: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows owner (`ctx.sender`) of a media entry to delete its `MediaMetadata` record using `object_key`.
- **Permissions**: Caller must have `UserProfile` and be `owner_identity` of the `MediaMetadata` entry.
- **Action**: Deletes the `MediaMetadata` record.

#### 3.7.4. `cleanup_stale_media_pending_uploads` (Scheduled)

(Located in `server/src/media_metadata.rs`, triggered by `MediaPendingUploadCleanupTimer`)

- **Function:** `cleanup_stale_media_pending_uploads(ctx: &ReducerContext, _timer: MediaPendingUploadCleanupTimer)`
- **Return Type:** `Result<(), String>`
- **Description:** Periodically deletes `MediaMetadata` entries older than 30 minutes (from `created_at`) if their `upload_status` is not `Available`. Must be called by scheduler.

### 3.8. Messaging

(Primarily from `server/src/messaging.rs`)

These reducers provide the core functionality for the messaging system.

#### 3.8.1. `send_direct_message`

- **Function:** `send_direct_message(ctx: &ReducerContext, recipients: Vec<Identity>, content: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Initiates a direct message. This is the primary entry point for starting new conversations or sending a message to a specific group of users for the first time. It calculates a `participants_hash` to find an existing conversation or creates a new one if none exists.
- **Permissions**: Caller must have a registered `UserProfile`.
- **Action**: Finds or creates a `Conversation` and `ConversationParticipant` records for all involved users (sender and recipients). Then, it calls `send_conversation_message` to post the actual message.

#### 3.8.2. `send_conversation_message`

- **Function:** `send_conversation_message(ctx: &ReducerContext, conversation_id: u64, content: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Sends a message to an existing conversation. This is more efficient for ongoing chats as it doesn't need to look up or create the conversation first.
- **Permissions**: Caller (`ctx.sender`) must be a participant in the specified `conversation_id`.
- **Action**: Creates a new `Message` record and updates the `last_message_at` timestamp on the `Conversation`.

#### 3.8.3. `add_user_to_conversation`

- **Function:** `add_user_to_conversation(ctx: &ReducerContext, conversation_id: u64, user_to_add: Identity)`
- **Return Type:** `Result<(), String>`
- **Description:** Adds a new user to an existing conversation.
- **Permissions**: Caller (`ctx.sender`) must be a current participant of the conversation.
- **Action**: Creates a new `ConversationParticipant` record for the `user_to_add`. Posts a `System` message to the conversation announcing that the user has been added.

#### 3.8.4. `leave_conversation`

- **Function:** `leave_conversation(ctx: &ReducerContext, conversation_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows the calling user to leave a conversation.
- **Permissions**: Caller (`ctx.sender`) must be a participant in the conversation.
- **Action**: Deletes the caller's `ConversationParticipant` record. Posts a `System` message announcing the user's departure. If the last participant leaves, the conversation's `status` is set to `Archived`.

#### 3.8.5. `rename_conversation`

- **Function:** `rename_conversation(ctx: &ReducerContext, conversation_id: u64, new_name: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Sets or changes the name of a conversation.
- **Permissions**: Caller (`ctx.sender`) must be a participant in the conversation.
- **Action**: Updates the `name` field in the `Conversation` table. Posts a `System` message announcing the rename.

#### 3.8.6. `edit_message`

- **Function:** `edit_message(ctx: &ReducerContext, message_id: u64, new_content: String)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user to edit the content of a message they previously sent.
- **Permissions**: Caller (`ctx.sender`) must be the original sender of the message. `System` messages cannot be edited.
- **Action**: Updates the `content` and `last_edited_at` fields on the `Message` record.

#### 3.8.7. `delete_message`

- **Function:** `delete_message(ctx: &ReducerContext, message_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Allows a user to delete a message they previously sent. This is a soft delete.
- **Permissions**: Caller (`ctx.sender`) must be the original sender of the message.
- **Action**: Sets the `is_deleted` flag on the `Message` record to `true`.

#### 3.8.8. `mark_conversation_as_read`

- **Function:** `mark_conversation_as_read(ctx: &ReducerContext, conversation_id: u64)`
- **Return Type:** `Result<(), String>`
- **Description:** Updates the user's `last_viewed_at` timestamp for a conversation. This allows clients to track unread messages.
- **Permissions**: Caller (`ctx.sender`) must be a participant in the conversation.
- **Action**: Updates the `last_viewed_at` field for the caller's `ConversationParticipant` record to the current `ctx.timestamp`.
