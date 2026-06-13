// file: last-round-backend/src/main/java/com/lastround/backend/service/LeaderboardService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.leaderboard.LeaderboardEntry;
import com.lastround.backend.entity.User;
import com.lastround.backend.repository.MatchRepository;
import com.lastround.backend.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.util.Comparator;
import java.util.List;

// Service that computes and returns the ranked player leaderboard.
@Service
@RequiredArgsConstructor
public class LeaderboardService {

    private final UserRepository userRepository;
    private final MatchRepository matchRepository;

    // Calculates win rate for each user and sorts by win rate desc, then wins desc, then matches desc.
    @Transactional(readOnly = true)
    public List<LeaderboardEntry> getLeaderboard() {
        List<User> users = userRepository.findAll();

        return users.stream()
                .map(user -> {
                    long totalMatches = matchRepository.countTotalByUserId(user.getId());
                    long totalWins = matchRepository.countByWinnerId(user.getId());
                    // Avoid division by zero; BigDecimal rounds to 2 decimal places for display.
                    double winRate = totalMatches == 0 ? 0.0
                            : BigDecimal.valueOf((double) totalWins * 100.0 / totalMatches)
                            .setScale(2, RoundingMode.HALF_UP)
                            .doubleValue();

                    return LeaderboardEntry.builder()
                            .userId(user.getId())
                            .nickname(user.getNickname())
                            .totalMatches(totalMatches)
                            .totalWins(totalWins)
                            .winRate(winRate)
                            .build();
                })
                .sorted(Comparator.comparing(LeaderboardEntry::getWinRate).reversed()
                        .thenComparing(LeaderboardEntry::getTotalWins, Comparator.reverseOrder())
                        .thenComparing(LeaderboardEntry::getTotalMatches, Comparator.reverseOrder()))
                .toList();
    }
}
