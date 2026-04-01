// file: last-round-backend/src/main/java/com/lastround/backend/service/MatchService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.match.MatchHistoryResponse;
import com.lastround.backend.dto.match.MatchResponse;
import com.lastround.backend.dto.match.MatchResultRequest;
import com.lastround.backend.entity.Match;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.MatchRepository;
import com.lastround.backend.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
@RequiredArgsConstructor
public class MatchService {

    private final MatchRepository matchRepository;
    private final UserRepository userRepository;

    @Transactional
    public MatchResponse saveResult(MatchResultRequest request) {
        validatePlayers(request);

        Match match = Match.builder()
                .player1Id(request.getPlayer1Id())
                .player2Id(request.getPlayer2Id())
                .winnerId(request.getWinnerId())
                .player1Score(request.getPlayer1Score())
                .player2Score(request.getPlayer2Score())
                .build();

        Match saved = matchRepository.save(match);
        return toResponse(saved);
    }

    @Transactional(readOnly = true)
    public MatchHistoryResponse getHistory(Long userId, int page, int size) {
        Page<Match> result = matchRepository.findByUserId(
                userId,
                PageRequest.of(page, size, Sort.by(Sort.Direction.DESC, "createdAt"))
        );

        List<MatchResponse> content = result.getContent().stream()
                .map(this::toResponse)
                .toList();

        return MatchHistoryResponse.builder()
                .content(content)
                .page(result.getNumber())
                .size(result.getSize())
                .totalElements(result.getTotalElements())
                .totalPages(result.getTotalPages())
                .build();
    }

    private void validatePlayers(MatchResultRequest request) {
        if (request.getPlayer1Id().equals(request.getPlayer2Id())) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Players must be different");
        }

        if (!request.getWinnerId().equals(request.getPlayer1Id()) && !request.getWinnerId().equals(request.getPlayer2Id())) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Winner must be one of the players");
        }

        if (!userRepository.existsById(request.getPlayer1Id()) || !userRepository.existsById(request.getPlayer2Id())) {
            throw new AppException(ErrorCode.USER_NOT_FOUND, "Player not found");
        }
    }

    private MatchResponse toResponse(Match match) {
        return MatchResponse.builder()
                .id(match.getId())
                .player1Id(match.getPlayer1Id())
                .player2Id(match.getPlayer2Id())
                .winnerId(match.getWinnerId())
                .player1Score(match.getPlayer1Score())
                .player2Score(match.getPlayer2Score())
                .createdAt(match.getCreatedAt())
                .build();
    }
}
