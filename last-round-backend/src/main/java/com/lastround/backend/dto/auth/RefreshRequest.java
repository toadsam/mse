// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/RefreshRequest.java
package com.lastround.backend.dto.auth;

import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class RefreshRequest {
    private String refreshToken;
}
