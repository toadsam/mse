// file: last-round-backend/src/main/java/com/lastround/backend/dto/user/UserUpdateRequest.java
package com.lastround.backend.dto.user;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

// Request DTO for the PUT /api/user/update endpoint. Currently only nickname is updatable.
@Getter
@Setter
public class UserUpdateRequest {

    @NotBlank
    @Size(min = 2, max = 30)
    private String nickname;
}
