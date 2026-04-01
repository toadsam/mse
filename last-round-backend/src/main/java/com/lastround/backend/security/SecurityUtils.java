// file: last-round-backend/src/main/java/com/lastround/backend/security/SecurityUtils.java
package com.lastround.backend.security;

import com.lastround.backend.exception.AppException;
import com.lastround.backend.exception.ErrorCode;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;

public final class SecurityUtils {

    private SecurityUtils() {
    }

    public static Long getCurrentUserId() {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null || !(authentication.getPrincipal() instanceof UserPrincipal principal)) {
            throw new AppException(ErrorCode.FORBIDDEN, "Authentication required");
        }
        return principal.getId();
    }
}
