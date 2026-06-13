// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/RefreshRequest.java
package com.lastround.backend.dto.auth;

import lombok.Getter;
import lombok.Setter;

// Optional request body for POST /api/auth/refresh.
// If omitted, the controller falls back to the refresh token stored in the HTTP-only cookie.
@Getter
@Setter
public class RefreshRequest {
    private String refreshToken;
}
