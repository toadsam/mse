// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/AuthResponse.java
package com.lastround.backend.dto.auth;

import lombok.Builder;
import lombok.Getter;

@Getter
@Builder
public class AuthResponse {
    private String accessToken;
    private String refreshToken;
    private Long userId;
    private String email;
    private String nickname;
}
