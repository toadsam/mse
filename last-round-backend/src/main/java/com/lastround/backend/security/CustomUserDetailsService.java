// file: last-round-backend/src/main/java/com/lastround/backend/security/CustomUserDetailsService.java
package com.lastround.backend.security;

import com.lastround.backend.entity.User;
import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import com.lastround.backend.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;

// Spring Security UserDetailsService implementation — loads user data for authentication.
@Service
@RequiredArgsConstructor
public class CustomUserDetailsService implements UserDetailsService {

    private final UserRepository userRepository;

    // Called by DaoAuthenticationProvider during login; username is the user's email.
    @Override
    public UserDetails loadUserByUsername(String username) throws UsernameNotFoundException {
        User user = userRepository.findByEmail(username)
                .orElseThrow(() -> new AppException(ErrorCode.USER_NOT_FOUND));
        return new UserPrincipal(user);
    }

    // Called by JwtAuthenticationFilter to populate the SecurityContext from a validated JWT.
    public UserDetails loadUserById(Long id) {
        User user = userRepository.findById(id)
                .orElseThrow(() -> new AppException(ErrorCode.USER_NOT_FOUND));
        return new UserPrincipal(user);
    }
}
