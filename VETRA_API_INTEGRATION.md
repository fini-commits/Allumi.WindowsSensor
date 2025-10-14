# 🔗 Allumi Windows Sensor - Vetra API Integration Guide

## 📡 API Endpoints

### Base Configuration
- **Supabase Project URL**: `https://lstannxhfhunacgkvtmm.supabase.co`
- **Base API URL**: `https://lstannxhfhunacgkvtmm.supabase.co/functions/v1`

---

## 🔑 1. Device Registration & Configuration

### Endpoint: `generate-user-config`
**Purpose**: Get user-specific configuration for Windows Sensor installation

- **Method**: `POST`
- **URL**: `https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/generate-user-config`
- **Authentication**: Requires JWT Bearer token (user must be logged in to web app first)

**Request Headers**:
```
Authorization: Bearer {USER_JWT_TOKEN}
Content-Type: application/json
```

**Response (200 OK)**:
```json
{
  "success": true,
  "config": {
    "apiKey": "alm_xxxxxxxxxxxxx",
    "userId": "uuid-here",
    "deviceId": "windows-{userId}-{timestamp}",
    "deviceName": "User's Windows Device",
    "syncUrl": "https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity",
    "supabaseUrl": "https://lstannxhfhunacgkvtmm.supabase.co",
    "supabaseAnonKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
}
```

**Important**: This endpoint automatically:
- Creates a device record in the database
- Generates and stores an API key
- Returns complete configuration for the Windows app

---

## 📤 2. Sync Device Activities

### Endpoint: `sync-device-activity`
**Purpose**: Upload tracked activities from Windows Sensor to Vetra

- **Method**: `POST`
- **URL**: `https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/sync-device-activity`
- **Authentication**: API Key (from config above)

**Request Headers**:
```
Content-Type: application/json
x-api-key: {API_KEY_FROM_CONFIG}
```

**Request Body**:
```json
{
  "apiKey": "alm_xxxxxxxxxxxxx",
  "activities": [
    {
      "deviceId": "windows-uuid-timestamp",
      "deviceName": "User's Windows Device",
      "appName": "Chrome.exe",
      "windowTitle": "GitHub - Allumi Project",
      "startTime": "2025-10-14T10:30:00Z",
      "endTime": "2025-10-14T10:45:00Z",
      "durationSeconds": 900,
      "category": "other",
      "isIdle": false
    }
  ],
  "deviceInfo": {
    "osVersion": "Windows 11 Pro",
    "syncFrequency": 60
  }
}
```

**Response (200 OK)**:
```json
{
  "success": true,
  "processed": 1,
  "message": "Activities synced and queued for AI categorization"
}
```

**Important Notes**:
- Activities are automatically categorized by AI after upload
- The `category` field can be set to "other" - AI will update it
- Device `last_seen` timestamp is automatically updated
- Batch multiple activities in one request for efficiency

---

## 🔧 3. Device Management

### Endpoint: `manage-devices`
**Purpose**: Register, list, or delete devices

- **Method**: `GET | POST | DELETE`
- **URL**: `https://lstannxhfhunacgkvtmm.supabase.co/functions/v1/manage-devices`
- **Authentication**: JWT Bearer token

### GET - List Devices

**Request Headers**:
```
Authorization: Bearer {USER_JWT_TOKEN}
```

**Response**:
```json
{
  "devices": [
    {
      "id": "windows-uuid-timestamp",
      "user_id": "user-uuid",
      "device_name": "User's Windows Device",
      "device_type": "desktop",
      "is_active": true,
      "last_seen": "2025-10-14T10:45:00Z",
      "os_version": "Windows 11 Pro",
      "sync_frequency_seconds": 60,
      "created_at": "2025-10-01T12:00:00Z",
      "device_api_keys": [
        {
          "api_key": "alm_xxxxx",
          "is_active": true,
          "created_at": "2025-10-01T12:00:00Z"
        }
      ]
    }
  ]
}
```

### POST - Register Device

**Request Headers**:
```
Authorization: Bearer {USER_JWT_TOKEN}
Content-Type: application/json
```

**Request Body**:
```json
{
  "deviceId": "unique-device-id",
  "deviceName": "My Windows PC",
  "deviceType": "desktop"
}
```

**Response**:
```json
{
  "success": true,
  "apiKey": "generated-api-key",
  "message": "Device registered successfully"
}
```

### DELETE - Remove Device

**Request Headers**:
```
Authorization: Bearer {USER_JWT_TOKEN}
Content-Type: application/json
```

**Request Body**:
```json
{
  "deviceId": "device-to-delete"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Device deleted successfully"
}
```

---

## 📊 Database Schema (Reference)

### Table: `devices`
```
- id (text, PK)
- user_id (uuid, FK)
- device_name (text)
- device_type (text) -- 'desktop', 'mobile', etc.
- is_active (boolean)
- last_seen (timestamp)
- os_version (text, nullable)
- sync_frequency_seconds (integer, default: 60)
- created_at (timestamp)
- updated_at (timestamp)
```

### Table: `device_api_keys`
```
- id (uuid, PK)
- user_id (uuid, FK)
- device_id (text, FK)
- api_key (text, unique)
- is_active (boolean)
- created_at (timestamp)
- expires_at (timestamp, nullable)
```

### Table: `device_activities`
```
- id (uuid, PK)
- user_id (uuid, FK)
- device_id (text, FK)
- device_name (text)
- device_type (text)
- app_name (text)
- window_title (text, nullable)
- start_time (timestamp)
- end_time (timestamp, nullable)
- duration_seconds (integer)
- category (text) -- AI-updated
- ai_subcategory (text, nullable)
- ai_confidence (integer, nullable)
- ai_reasoning (text, nullable)
- productivity_score (integer, nullable)
- is_idle (boolean)
- created_at (timestamp)
- updated_at (timestamp)
```

---

## 🎯 Recommended Implementation Flow

### 1. Initial Setup (One-time)
```
User downloads Windows Sensor installer
  ↓
Installer contains config.json with:
  - apiKey
  - deviceId
  - syncUrl
  - userId
```

### 2. Runtime Flow
```
Windows Sensor starts
  ↓
Loads config.json
  ↓
Tracks user activities (apps, windows, time)
  ↓
Every 60 seconds (configurable):
  - Batch activities
  - POST to sync-device-activity endpoint
  - Update last_seen timestamp
```

### 3. Sync Frequency
- **Recommended**: Every 60 seconds
- **Configurable**: Via `sync_frequency_seconds` in device record
- **Batch size**: 50-100 activities per request optimal

---

## 🔒 Security Notes

- **API Key Storage**: Store in encrypted config file
- **HTTPS Only**: All requests must use HTTPS
- **API Key Format**: `alm_` prefix + random string
- **Rate Limiting**: Not currently enforced, but recommend max 1 request/min
- **Token Expiry**: API keys don't expire unless manually revoked

---

## 🐛 Error Handling

### Common Error Responses

**401 Unauthorized**:
```json
{
  "error": "Invalid or expired API key"
}
```

**400 Bad Request**:
```json
{
  "error": "Missing required fields: apiKey, activities"
}
```

**500 Internal Server Error**:
```json
{
  "error": "Failed to save activities"
}
```

### Retry Logic Recommendations
- Retry on 500 errors with exponential backoff
- Don't retry on 401 (invalid key)
- Queue activities locally if sync fails
- Max retry attempts: 3

---

## 📞 Questions or Issues?

Contact the Vetra development team or check the main repository for updates.

**Document Version**: 1.0  
**Last Updated**: 2025-10-14  
**Vetra Project ID**: lstannxhfhunacgkvtmm
