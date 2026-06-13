// file: last-round-backend/src/main/java/com/lastround/backend/dto/auth/LoginRequest.java
package com.lastround.backend.dto.auth;

import jakarta.validation.constraints.NotBlank;
import lombok.Getter;
import lombok.Setter;

// Request DTO for the POST /api/auth/login endpoint.
@Getter
@Setter
public class LoginRequest {

    @NotBlank
    private String email;

    @NotBlank
    private String password;
}
