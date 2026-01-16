import NextAuth from "next-auth"
import { JWT } from "next-auth/jwt";
import Keycloak from "next-auth/providers/keycloak"

declare module "next-auth" {
  interface Session {
    accessToken: string
    error?: string;
  }
}

declare module "@auth/core/jwt" {
  interface JWT {
    accessToken: string;
    accessTokenExpires: number;
    refreshToken: string;
    error?: string;
  }
}

const refreshAccessToken = async (token: JWT) => {
  try {
    const url =
      `${process.env.KEYCLOAK_ISSUER}/protocol/openid-connect/token` +
      new URLSearchParams({
        client_id: process.env.KEYCLOAK_CLIENT_ID!,
        client_secret: process.env.KEYCLOAK_CLIENT_SECRET!,
        grant_type: "refresh_token",
        refresh_token: token.refreshToken,
      });

    const response = await fetch(url, {
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      method: "POST",
    });

    const refreshedTokens = await response.json()

    if (!response.ok) {
      throw refreshedTokens
    }

    return {
      ...token,
      accessToken: refreshedTokens.access_token,
      accessTokenExpires: Date.now() + refreshedTokens.expires_in * 1000,
      refreshToken: refreshedTokens.refresh_token ?? token.refreshToken, // Fall back to old refresh token
    }
  } catch (error) {
    // TODO: Do logging
    return {
      ...token,
      error: "RefreshAccessTokenError",
    }
  }
}

export const { handlers, signIn, signOut, auth } = NextAuth({
  providers: [
    Keycloak({
      clientId: process.env.KEYCLOAK_CLIENT_ID,
      clientSecret: process.env.KEYCLOAK_CLIENT_SECRET,
      issuer: process.env.KEYCLOAK_ISSUER,
    }),
  ],
  callbacks: {
    async jwt({ token, account }) {
      if (account) {
        return {
          accessToken: account.access_token,
          accessTokenExpires: Date.now() + account.expires_in! * 1000,
          refreshToken: account.refresh_token,
        }
      }

      if (Date.now() < token.accessTokenExpires) {
        return token
      }

      return refreshAccessToken(token)
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken
      session.error = token.error

      return session
    },
  },
})
