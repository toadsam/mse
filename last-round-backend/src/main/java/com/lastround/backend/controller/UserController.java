// file: last-round-backend/src/main/java/com/lastround/backend/controller/UserController.java
package com.lastround.backend.controller;

import com.lastround.backend.dto.ApiResponse;
import com.lastround.backend.dto.user.UserMeResponse;
import com.lastround.backend.dto.user.UserUpdateRequest;
import com.lastround.backend.security.SecurityUtils;
import com.lastround.backend.service.UserService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

// REST controller for the authenticated user's own profile operations.
@RestController
@RequestMapping("/api/user")
@RequiredArgsConstructor
public class UserController {

    private final UserService userService;

    // GET /api/user/me — returns profile information for the currently authenticated user.
    @GetMapping("/me")
    public ApiResponse<UserMeResponse> me() {
        return ApiResponse.ok(userService.getMe(SecurityUtils.getCurrentUserId()));
    }

    // PUT /api/user/update — updates mutable profile fields (e.g., nickname) for the current user.
    @PutMapping("/update")
    public ApiResponse<UserMeResponse> update(@Valid @RequestBody UserUpdateRequest request) {
        return ApiResponse.ok(userService.update(SecurityUtils.getCurrentUserId(), request));
    }
}
