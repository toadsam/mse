// file: last-round-backend/src/main/java/com/lastround/backend/dto/user/UserMeResponse.java
package com.lastround.backend.dto.user;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;

@Getter
@Builder
public class UserMeResponse {
    private Long id;
    private String email;
    private String nickname;
    private LocalDateTime createdAt;
}
