// file: last-round-backend/src/main/java/com/lastround/backend/controller/MatchController.java
package com.lastround.backend.controller;

import com.lastround.backend.dto.ApiResponse;
import com.lastround.backend.dto.match.MatchHistoryResponse;
import com.lastround.backend.dto.match.MatchResponse;
import com.lastround.backend.dto.match.MatchResultRequest;
import com.lastround.backend.security.SecurityUtils;
import com.lastround.backend.service.MatchService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/match")
@RequiredArgsConstructor
public class MatchController {

    private final MatchService matchService;

    @PostMapping("/result")
    public ApiResponse<MatchResponse> result(@Valid @RequestBody MatchResultRequest request) {
        return ApiResponse.ok(matchService.saveResult(request));
    }

    @GetMapping("/history")
    public ApiResponse<MatchHistoryResponse> history(
            @RequestParam(defaultValue = "0") int page,
            @RequestParam(defaultValue = "20") int size
    ) {
        return ApiResponse.ok(matchService.getHistory(SecurityUtils.getCurrentUserId(), page, size));
    }
}
