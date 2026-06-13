// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/AuthResponse.java
package com.lastround.backend.dto.auth;

import lombok.Builder;
import lombok.Getter;

// Response DTO returned after successful signup, login, or token refresh.
// The refresh token is also set as an HTTP-only cookie by the controller.
@Getter
@Builder
public class AuthResponse {
    private String accessToken;
    private String refreshToken;
    private Long userId;
    private String email;
    private String nickname;
}
