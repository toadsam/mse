// file: last-round-backend/src/main/java/com/lastround/backend/controller/AugmentController.java
package com.lastround.backend.controller;

import com.lastround.backend.dto.ApiResponse;
import com.lastround.backend.dto.augment.AugmentResponse;
import com.lastround.backend.service.AugmentService;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

// REST controller exposing the augment catalog. Public endpoint — no authentication required.
@RestController
@RequestMapping("/api/augments")
@RequiredArgsConstructor
public class AugmentController {

    private final AugmentService augmentService;

    // GET /api/augments — returns the full list of available augments.
    @GetMapping
    public ApiResponse<List<AugmentResponse>> getAugments() {
        return ApiResponse.ok(augmentService.getAugments());
    }
}
