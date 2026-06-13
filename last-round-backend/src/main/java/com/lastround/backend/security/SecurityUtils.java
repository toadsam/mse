// file: last-round-backend/src/main/java/com/lastround/backend/security/SecurityUtils.java
package com.lastround.backend.security;

import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;

// Utility class for accessing the authenticated user from the Spring Security context.
public final class SecurityUtils {

    private SecurityUtils() {
    }

    // Retrieves the current user's ID from the SecurityContext; throws FORBIDDEN if not authenticated.
    public static Long getCurrentUserId() {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null || !(authentication.getPrincipal() instanceof UserPrincipal principal)) {
            throw new AppException(ErrorCode.FORBIDDEN, "Authentication required");
        }
        return principal.getId();
    }
}
