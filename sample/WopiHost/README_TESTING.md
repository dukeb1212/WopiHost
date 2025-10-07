## Testing WOPI Host Integration

### Prerequisites
1. PostgreSQL database running with connection string configured
2. File storage directory exists (configured in FileStorageSettings:RootPath)
3. Sample files in database table `section0.cr02tepdinhkem`

### Test Endpoints

#### 1. Get Action URL for a file
```bash
# Get view URL for file with ID 1
curl -X GET "https://office.digifact.vn/api/action/27?action=view&userId=3"

# Get edit URL for file with ID 1  
curl -X GET "https://office.digifact.vn/api/action/12?action=edit"
```

#### 2. WOPI CheckFileInfo
```bash
# Replace {access_token} with token from action URL response
curl -X GET "https://office.digifact.vn/wopi/files/12?access_token={access_token}"
```

#### 3. WOPI GetFile
```bash
# Download file content
curl -X GET "https://office.digifact.vn/wopi/files/12/contents?access_token={access_token}" --output downloaded_file.docx
```

#### 4. WOPI PutFile
```bash
# Upload new file content
curl -X POST "https://office.digifact.vn/wopi/files/12/contents?access_token={access_token}" \
     -H "Content-Type: application/octet-stream" \
     --data-binary @updated_file.docx
```

### Database Setup

If you need to create sample data:

```sql
-- Create sample file record
INSERT INTO section0.cr02tepdinhkem (
    id, active, version, createdate, writedate,
    iddoituong, tenbang, remotepath, filename, 
    fileextension, sizeinbytes, mimetype, filecategory
) VALUES (
    1, 1, '1.0', NOW(), NOW(),
    NULL, 'test_table', 'documents', 'sample.docx',
    '.docx', 1024, 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'office'
);
```

### Expected Flow for WPF Integration

1. WPF app calls `/api/action/{fileId}?action=edit` to get Office Online URL
2. WPF opens WebView2 with the returned ActionUrl
3. Office Online calls WOPI endpoints to get file info and content
4. User edits document in Office Online
5. Office Online saves changes back via WOPI PutFile endpoint
6. File content and metadata updated in database

### Configuration Notes

Update `appsettings.json` with your specific values:
- PostgreSQL connection string
- File storage root path  
- Office Online Server discovery URL
- JWT signing key (should be at least 32 characters)
