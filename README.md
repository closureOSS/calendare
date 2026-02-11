# Calendare Server

Open-Source Calendar and contacts server (CalDAV/CardDAV)

## Features

The **Calendare Server** is built on open standards to ensure seamless compatibility with your favorite calendar and contact applications. Key capabilities include:

- **Full Calendar & Contact Sync**: Comprehensive support for CalDAV and CardDAV, allowing you to sync events and address books across all your devices.
- **Self-Hosted & Privacy-First**: By hosting your own Calendare Server, you maintain **complete ownership of your data**. Your personal schedules and contact lists remain on your infrastructure, protected from third-party data mining.
- **Availability Management**: Includes "Free/Busy" support, helping you manage your schedule and share your availability with others.
- **Efficient Synchronization**: High-performance syncing that only downloads changes since your last update, saving battery life and data.
- **Real-Time Push Notifications**: Support for modern Web Push standards (including encrypted notifications and VAPID keys), so your devices stay updated instantly without constant polling.

For the full list of supported RFC's and other specification see the [implemented scope](./Doc/SCOPE.md).

## Designed for Collaboration & Scalability

Unlike lightweight solutions designed for individual use, **Calendare Server** is built specifically for organizations, such as small-to-medium-sized businesses, clubs, and teams.

While this requires a more robust infrastructure — including a **PostgreSQL database** and an **OIDC provider** — it enables professional-grade features that individual setups lack:

- **Comprehensive Permission System**: Granular access control for users and teams.

- **Group Calendars**: Seamlessly coordinate schedules across entire departments or organizations.

- **Resource Management**: Native support for booking shared resources, such as meeting rooms and equipment.

Optimized for modern DevOps workflows, the recommended deployment target is a **Kubernetes cluster**, ensuring high availability and professional management of your organization's data.

## Quickstart

To start see the [installation guide](./Doc/INSTALL.md).

## Development

See [development notes](./Doc/DEVELOPMENT.md).
