# Implementation scope

## Supported RFC's

- [RFC 4791 - Calendaring Extensions to WebDAV (CalDAV)](https://datatracker.ietf.org/doc/html/rfc4791)
- [RFC 5397 - WebDAV Current Principal Extension](https://datatracker.ietf.org/doc/html/rfc5397.html)
- [RFC 5689 - Extended MKCOL for Web Distributed Authoring and Versioning (WebDAV)](https://datatracker.ietf.org/doc/html/rfc5689)
- [RFC 5995 - Using POST to Add Members to Web Distributed Authoring and Versioning (WebDAV) Collections](https://datatracker.ietf.org/doc/html/rfc5995)
- [RFC 6350 - vCard Format Specification](https://datatracker.ietf.org/doc/html/rfc6350)
  - By external library
- [RFC 6352 - CardDAV: vCard Extensions to Web Distributed Authoring and Versioning (WebDAV)](https://datatracker.ietf.org/doc/html/rfc6352)
  - [No support for CARDDAV:prop in CARDDAV:address-data](https://datatracker.ietf.org/doc/html/rfc6352#section-10.4.2)
  - [No explicit support for CARDDAV:allprop in CARDDAV:address-data, allprop is always assumed](https://datatracker.ietf.org/doc/html/rfc6352#section-10.4.1)
  - No support for truncation of results (CARDDAV:limit)
- [RFC 6578 - Collection Synchronization for Web Distributed Authoring and Versioning (WebDAV)](https://datatracker.ietf.org/doc/html/rfc6578)
  - No support for sync-level Infinite (DAV:sync-level = infinite)
  - No support for truncation of results (DAV:limit)
- [RFC 6764 - Locating Services for Calendaring Extensions to WebDAV (CalDAV) and vCard Extensions to WebDAV (CardDAV)](https://datatracker.ietf.org/doc/html/rfc6764)
  - DNS entries SRV/TXT \_carddavs.\_tcp and \_caldavs.\_tcp
- [RFC 7953 - Calendar Availability](https://datatracker.ietf.org/doc/html/rfc7953)
- [Draft specification for WebDAV Push](https://github.com/bitfireAT/webdav-push), for [HTTP Push](https://datatracker.ietf.org/doc/html/rfc8030), [Message encryption for Web Push](https://datatracker.ietf.org/doc/html/rfc8291), [VAPID keys](https://datatracker.ietf.org/doc/html/rfc8292)

### iCalendar specification

- [RFC 5545 - Internet Calendaring and Scheduling Core Object Specification (iCalendar)](https://datatracker.ietf.org/doc/html/rfc5545)
- [RFC 6868 - Parameter Value Encoding in iCalendar and vCard](https://datatracker.ietf.org/doc/html/rfc6868)
- [RFC 7986 - New Properties for iCalendar](https://datatracker.ietf.org/doc/html/rfc7986)
- [RFC 9073 - Event Publishing Extensions to iCalendar](https://datatracker.ietf.org/doc/html/rfc9073)
- [RFC 9074 - "VALARM" Extensions for iCalendar](https://datatracker.ietf.org/doc/html/rfc9074)
- [RFC 9253 - Support for iCalendar Relationships](https://datatracker.ietf.org/doc/html/rfc9253)

## RFC's under consideration

- [WebDAV Resource Sharing](https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04)
- [RFC 3744 - Web Distributed Authoring and Versioning (WebDAV) Access Control Protocol ](https://datatracker.ietf.org/doc/html/rfc3744)
- [RFC 5546 - iCalendar Transport-Independent Interoperability Protocol (iTIP)](https://datatracker.ietf.org/doc/html/rfc5546)
- [RFC 6047 - iCalendar Message-Based Interoperability Protocol (iMIP)](https://datatracker.ietf.org/doc/html/rfc6047)
- [RFC 6638 - Scheduling Extensions to CalDAV](https://datatracker.ietf.org/doc/html/rfc6638)
- [RFC 7809 - Calendaring Extensions to WebDAV (CalDAV): Time Zones by Reference](https://datatracker.ietf.org/doc/html/rfc7809)
- [RFC 5842 - Binding Extensions to Web Distributed Authoring and Versioning (WebDAV)](https://datatracker.ietf.org/doc/html/rfc5842)
- [WebDAV: User Notifications (Draft 03, 2016)](https://datatracker.ietf.org/doc/html/draft-pot-webdav-notifications-03)

## Limited support

- [RFC 4918 - HTTP Extensions for Web Distributed Authoring and Versioning (WebDAV)](https://datatracker.ietf.org/doc/html/rfc4918)
  - Limited support related to the function as calendar and addressbook server
  - No support for Locking (No Class 2 compliance)
- [RFC 5323 - Web Distributed Authoring and Versioning (WebDAV) SEARCH](https://datatracker.ietf.org/doc/html/rfc5323)
  - [DAV:limit and DAV:nresults](https://datatracker.ietf.org/doc/html/rfc5323#section-5.17)
- [RFC 3253 - Versioning Extensions to WebDAV (Web Distributed Authoring and Versioning)](https://datatracker.ietf.org/doc/html/rfc3253)
  - [DAV:supported-method-set](https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3)
  - [DAV:supported-report-set](https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.5)

## Known unsupported

- [Calendaring Extensions to WebDAV (CalDAV): Managed Attachments](https://datatracker.ietf.org/doc/html/rfc8607)

# Further references

- [Collection of relevant RFC's](https://greenbytes.de/tech/webdav/)
- [Google CalDAV Api developer's guide](https://developers.google.com/calendar/caldav/v2/guide?hl=en)
- [Summary of CalDAV](https://www.aurinko.io/blog/caldav-apple-calendar-integration/), [SABRE CalDAV](https://sabre.io/dav/building-a-caldav-client/) or [SABRE CardDAV](https://sabre.io/dav/building-a-carddav-client/)
- [Summary of Discovery](https://www.atmail.com/blog/caldav-carddav/)
- [General](https://wiki.wocommunity.org/xwiki/bin/view/~probert/Home/CalDAV%20and%20CardDAV/CalDAV%20and%20CardDAV%20properties/)
- [DAVx5 Synchronization Logic](https://manual.davx5.com/technical_information.html)
