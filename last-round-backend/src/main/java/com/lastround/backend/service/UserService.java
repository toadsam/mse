// file: last-round-backend/src/main/java/com/lastround/backend/service/UserService.java
package com.lastround.backend.service;

import com.lastround.backend.dto.user.UserMeResponse;
import com.lastround.backend.dto.user.UserUpdateRequest;
import com.lastround.backend.entity.User;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class UserService {

    private final UserRepository userRepository;

    @Transactional(readOnly = true)
    public UserMeResponse getMe(Long userId) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new AppException(ErrorCode.USER_NOT_FOUND));

        return UserMeResponse.builder()
                .id(user.getId())
                .email(user.getEmail())
                .nickname(user.getNickname())
                .createdAt(user.getCreatedAt())
                .build();
    }

    @Transactional
    public UserMeResponse update(Long userId, UserUpdateRequest request) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new AppException(ErrorCode.USER_NOT_FOUND));

        if (!user.getNickname().equals(request.getNickname()) && userRepository.existsByNickname(request.getNickname())) {
            throw new AppException(ErrorCode.NICKNAME_ALREADY_EXISTS);
        }

        user.setNickname(request.getNickname());

        return UserMeResponse.builder()
                .id(user.getId())
                .email(user.getEmail())
                .nickname(user.getNickname())
                .createdAt(user.getCreatedAt())
                .build();
    }
}
