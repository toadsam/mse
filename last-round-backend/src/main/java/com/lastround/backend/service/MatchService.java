// file: last-round-backend/src/main/java/com/lastround/backend/service/MatchService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.match.MatchPlayerResultRequest;
import com.lastround.backend.dto.match.MatchHistoryResponse;
import com.lastround.backend.dto.match.MatchPlayerAugmentResponse;
import com.lastround.backend.dto.match.MatchPlayerHistoryResponse;
import com.lastround.backend.dto.match.MatchResponse;
import com.lastround.backend.dto.match.MatchResultRequest;
import com.lastround.backend.entity.Augment;
import com.lastround.backend.entity.Match;
import com.lastround.backend.entity.MatchPlayerAugment;
import com.lastround.backend.entity.MatchPlayerStat;
import com.lastround.backend.entity.User;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.AugmentRepository;
import com.lastround.backend.repository.MatchPlayerAugmentRepository;
import com.lastround.backend.repository.MatchPlayerStatRepository;
import com.lastround.backend.repository.MatchRepository;
import com.lastround.backend.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class MatchService {

    private final AugmentRepository augmentRepository;
    private final MatchRepository matchRepository;
    private final MatchPlayerStatRepository matchPlayerStatRepository;
    private final MatchPlayerAugmentRepository matchPlayerAugmentRepository;
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
            persistPlayerDetails(saved, request);
        return toResponse(saved);
    }

    @Transactional(readOnly = true)
    public MatchHistoryResponse getHistory(Long userId, int page, int size) {
        Page<Match> result = matchRepository.findByUserId(
                userId,
                PageRequest.of(page, size, Sort.by(Sort.Direction.DESC, "createdAt"))
        );

        List<Long> matchIds = result.getContent().stream()
            .map(Match::getId)
            .toList();

        Map<Long, List<MatchPlayerHistoryResponse>> playersByMatchId = loadPlayersByMatchId(matchIds);

        List<MatchResponse> content = result.getContent().stream()
            .map(match -> toResponse(match, playersByMatchId.getOrDefault(match.getId(), List.of())))
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

        validatePlayerPayload(request);
    }

    private void validatePlayerPayload(MatchResultRequest request) {
        if (request.getPlayers().size() != 2) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Exactly two player results are required");
        }

        Set<Long> expectedUserIds = Set.of(request.getPlayer1Id(), request.getPlayer2Id());
        Set<Long> actualUserIds = request.getPlayers().stream()
                .map(player -> player.getUserId())
                .collect(java.util.stream.Collectors.toSet());

        if (!actualUserIds.equals(expectedUserIds)) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Player results must match player1Id and player2Id");
        }

        request.getPlayers().forEach(player -> validatePlayerResult(request, player));
    }

    private void validatePlayerResult(MatchResultRequest request, MatchPlayerResultRequest player) {
        Integer expectedScore = player.getUserId().equals(request.getPlayer1Id())
                ? request.getPlayer1Score()
                : request.getPlayer2Score();

        if (!expectedScore.equals(player.getScore())) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Player score must match the match summary");
        }

        String normalizedResult = player.getResult().toUpperCase(Locale.ROOT);
        boolean isWinner = player.getUserId().equals(request.getWinnerId());
        if (isWinner && !"WIN".equals(normalizedResult)) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Winning player result must be WIN");
        }
        if (!isWinner && !"LOSE".equals(normalizedResult)) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "Losing player result must be LOSE");
        }

        Set<String> augmentSlots = new HashSet<>();
        player.getAugments().forEach(augment -> {
            String slotKey = augment.getSelectedRound() + ":" + augment.getSelectedOrder();
            if (!augmentSlots.add(slotKey)) {
                throw new AppException(ErrorCode.INVALID_REQUEST, "Duplicate augment selection order in the same round");
            }
        });

        validateAugmentIds(player);
        }

        private void validateAugmentIds(MatchPlayerResultRequest player) {
        // augmentId는 선택값이다(Unity augment가 DB augments에 없을 수 있음).
        // 값이 채워진 id(>0)만 존재 여부를 검증하고, 없으면 이름만으로 저장한다.
        // (Unity JsonUtility는 id를 항상 0으로 직렬화하므로 0/음수는 "id 없음"으로 간주.)
        Set<Long> augmentIds = player.getAugments().stream()
            .map(augment -> augment.getAugmentId())
            .filter(id -> id != null && id > 0)
            .collect(Collectors.toSet());

        if (augmentIds.isEmpty()) {
            return;
        }

        Set<Long> existingAugmentIds = augmentRepository.findAllById(augmentIds).stream()
            .map(Augment::getId)
            .collect(Collectors.toSet());

        if (!existingAugmentIds.equals(augmentIds)) {
            throw new AppException(ErrorCode.INVALID_REQUEST, "One or more augment IDs are invalid");
        }
        }

        private void persistPlayerDetails(Match savedMatch, MatchResultRequest request) {
        Map<Long, User> usersById = userRepository.findAllById(
                request.getPlayers().stream().map(MatchPlayerResultRequest::getUserId).toList()
            ).stream()
            .collect(Collectors.toMap(User::getId, Function.identity()));

        Set<Long> augmentIds = request.getPlayers().stream()
            .flatMap(player -> player.getAugments().stream())
            .map(augment -> augment.getAugmentId())
            .filter(id -> id != null && id > 0)
            .collect(Collectors.toSet());

        Map<Long, Augment> augmentsById = augmentRepository.findAllById(augmentIds).stream()
            .collect(Collectors.toMap(Augment::getId, Function.identity()));

        List<MatchPlayerStat> playerStats = request.getPlayers().stream()
            .map(player -> MatchPlayerStat.builder()
                .match(savedMatch)
                .user(usersById.get(player.getUserId()))
                .result(player.getResult().toUpperCase(Locale.ROOT))
                .score(player.getScore())
                .damageDealt(player.getDamageDealt())
                .characterName(player.getCharacterName())
                .build())
            .toList();
        matchPlayerStatRepository.saveAll(playerStats);

        List<MatchPlayerAugment> playerAugments = request.getPlayers().stream()
            .flatMap(player -> player.getAugments().stream()
                .map(augment -> MatchPlayerAugment.builder()
                    .match(savedMatch)
                    .user(usersById.get(player.getUserId()))
                    .augment(augment.getAugmentId() == null || augment.getAugmentId() <= 0 ? null : augmentsById.get(augment.getAugmentId()))
                    .augmentName(augment.getAugmentName())
                    .selectedRound(augment.getSelectedRound())
                    .selectedOrder(augment.getSelectedOrder())
                    .build()))
            .toList();

        if (!playerAugments.isEmpty()) {
            matchPlayerAugmentRepository.saveAll(playerAugments);
        }
    }

    private MatchResponse toResponse(Match match) {
        return toResponse(match, List.of());
        }

        private MatchResponse toResponse(Match match, List<MatchPlayerHistoryResponse> players) {
        return MatchResponse.builder()
                .id(match.getId())
                .player1Id(match.getPlayer1Id())
                .player2Id(match.getPlayer2Id())
                .winnerId(match.getWinnerId())
                .player1Score(match.getPlayer1Score())
                .player2Score(match.getPlayer2Score())
                .createdAt(match.getCreatedAt())
            .players(players)
                .build();
    }

        private Map<Long, List<MatchPlayerHistoryResponse>> loadPlayersByMatchId(List<Long> matchIds) {
        if (matchIds.isEmpty()) {
            return Map.of();
        }

        List<MatchPlayerStat> stats = matchPlayerStatRepository.findAllByMatchIds(matchIds);
        List<MatchPlayerAugment> augments = matchPlayerAugmentRepository.findAllByMatchIds(matchIds);

        Map<Long, Map<Long, List<MatchPlayerAugmentResponse>>> augmentsByMatchAndUser = augments.stream()
            .collect(Collectors.groupingBy(
                augment -> augment.getMatch().getId(),
                LinkedHashMap::new,
                Collectors.groupingBy(
                    augment -> augment.getUser().getId(),
                    LinkedHashMap::new,
                    Collectors.mapping(this::toAugmentResponse, Collectors.toList())
                )
            ));

        return stats.stream()
            .collect(Collectors.groupingBy(
                stat -> stat.getMatch().getId(),
                LinkedHashMap::new,
                Collectors.mapping(
                    stat -> toPlayerHistoryResponse(
                        stat,
                        augmentsByMatchAndUser
                            .getOrDefault(stat.getMatch().getId(), Map.of())
                            .getOrDefault(stat.getUser().getId(), List.of())
                    ),
                    Collectors.toList()
                )
            ));
        }

        private MatchPlayerHistoryResponse toPlayerHistoryResponse(
            MatchPlayerStat stat,
            List<MatchPlayerAugmentResponse> augments
        ) {
        return MatchPlayerHistoryResponse.builder()
            .userId(stat.getUser().getId())
            .nickname(stat.getUser().getNickname())
            .result(stat.getResult())
            .score(stat.getScore())
            .damageDealt(stat.getDamageDealt())
            .characterName(stat.getCharacterName())
            .createdAt(stat.getCreatedAt())
            .augments(augments)
            .build();
        }

        private MatchPlayerAugmentResponse toAugmentResponse(MatchPlayerAugment augment) {
        // augment FK는 null일 수 있다(Unity augment가 DB에 없는 경우). 이름 컬럼을 우선 사용한다.
        Augment linked = augment.getAugment();
        String name = augment.getAugmentName() != null ? augment.getAugmentName()
                : (linked != null ? linked.getName() : null);
        return MatchPlayerAugmentResponse.builder()
            .augmentId(linked != null ? linked.getId() : null)
            .augmentName(name)
            .effectType(linked != null ? linked.getEffectType() : null)
            .selectedOrder(augment.getSelectedOrder())
            .selectedRound(augment.getSelectedRound())
            .build();
        }
}
