// file: last-round-backend/src/main/java/com/lastround/backend/controller/LeaderboardController.java
package com.lastround.backend.controller;

import com.lastround.backend.dto.ApiResponse;
import com.lastround.backend.dto.leaderboard.LeaderboardEntry;
import com.lastround.backend.service.LeaderboardService;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/leaderboard")
@RequiredArgsConstructor
public class LeaderboardController {

    private final LeaderboardService leaderboardService;

    @GetMapping
    public ApiResponse<List<LeaderboardEntry>> getLeaderboard() {
        return ApiResponse.ok(leaderboardService.getLeaderboard());
    }
}
