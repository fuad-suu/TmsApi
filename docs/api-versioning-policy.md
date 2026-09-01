# TMS API Versioning Policy

## 1. Overview

This policy establishes how the Training Management System (TMS) API handles interface evolution, deprecation, and sunsetting to guarantee backward compatibility for client integrations.

## 2. Breaking vs. Non-Breaking Changes

### Breaking Changes (Requires New Major Version)

- Renaming or removing existing fields, endpoints, or parameters.
- Changing data types, response structures, or HTTP status codes.
- Tightening validation rules or changing default query/sorting behavior.

### Additive / Non-Breaking Changes (Pushed to Existing Version)

- Adding new optional request fields or optional query parameters.
- Adding new endpoints or expanding response payloads with new optional properties.

## 3. Versioning Strategy

- **URL Segment Versioning**: All production routes must be prefixed with `/api/v{major}/` (e.g., `/api/v1/courses`, `/api/v2/courses`).
- **Header Reporting**: All responses include the `api-supported-versions` header listing active major versions.

## 4. Deprecation & Sunset Timeline

- **Minimum Sunset Window**: Deprecated API versions are maintained for a minimum of **6 months** before removal.
- **Deprecation Signals**:
  - `Deprecation: true` header attached to all responses on deprecated versions.
  - `Sunset: <RFC 7231 Date>` header communicating the exact decommission date.
  - `Link: </api/vX/...>; rel="successor-version"` header providing the direct target migration route.

## 5. Version Skipping

Clients may migrate directly from `V1` to `V3` without migrating through intermediate versions (`V2`).
