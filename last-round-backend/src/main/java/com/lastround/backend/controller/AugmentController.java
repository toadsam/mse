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

@RestController
@RequestMapping("/api/augments")
@RequiredArgsConstructor
public class AugmentController {

    private final AugmentService augmentService;

    @GetMapping
    public ApiResponse<List<AugmentResponse>> getAugments() {
        return ApiResponse.ok(augmentService.getAugments());
    }
}
