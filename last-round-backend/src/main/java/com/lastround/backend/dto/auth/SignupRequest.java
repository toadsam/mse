// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/SignupRequest.java
package com.lastround.backend.dto.auth;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

// Request DTO for the POST /api/auth/signup endpoint.
@Getter
@Setter
public class SignupRequest {

    @NotBlank
    private String email;

    // Password must be 8–64 characters to enforce a basic strength requirement.
    @NotBlank
    @Size(min = 8, max = 64)
    private String password;

    // Nickname displayed in-game and on the leaderboard.
    @NotBlank
    @Size(min = 2, max = 30)
    private String nickname;
}
