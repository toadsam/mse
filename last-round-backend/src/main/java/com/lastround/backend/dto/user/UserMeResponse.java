// file: last-round-backend/src/main/java/com/lastround/backend/dto/user/UserMeResponse.java
package com.lastround.backend.dto.user;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;

// Response DTO returned for the GET /api/user/me endpoint.
@Getter
@Builder
public class UserMeResponse {
    private Long id;
    private String email;
    private String nickname;
    private LocalDateTime createdAt;
}
