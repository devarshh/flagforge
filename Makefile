# Convenience targets. The README lists the equivalent commands for people without make.
.DEFAULT_GOAL := help
.PHONY: help up down logs build test test-backend test-frontend lint format smoke k8s-up k8s-down

COMPOSE ?= docker compose
FRONTEND := src/frontend
SMOKE := tests/smoke

help: ## List the targets
	@awk 'BEGIN { FS = ":.*## " } /^[a-z0-9-]+:.*## / { printf "  %-14s %s\n", $$1, $$2 }' $(MAKEFILE_LIST)

up: ## Build and start everything (dashboard http://localhost:8080, demo /demo/)
	$(COMPOSE) up --build -d

down: ## Stop everything and delete the volumes (the local database)
	$(COMPOSE) down -v

logs: ## Follow the logs of every service
	$(COMPOSE) logs -f

build: ## Build the container images
	$(COMPOSE) build

test: test-backend test-frontend ## Run all backend and frontend tests

test-backend: ## Run the .NET tests (needs Docker for Testcontainers)
	dotnet test --solution FlagForge.slnx

test-frontend: ## Run the SDK, dashboard, and demo tests
	cd $(FRONTEND) && { [ -d node_modules ] || npm ci; } && npm test

lint: ## Check formatting and lint rules (.NET and frontend)
	dotnet format FlagForge.slnx --verify-no-changes
	cd $(FRONTEND) && { [ -d node_modules ] || npm ci; } && npm run lint

format: ## Fix formatting (.NET and frontend)
	dotnet format FlagForge.slnx
	cd $(FRONTEND) && { [ -d node_modules ] || npm ci; } && npm run format

smoke: ## Smoke-test a running stack (BASE_URL, SMOKE_MODE=full|readonly, ADMIN_EMAIL, ...)
	cd $(SMOKE) && { [ -d node_modules ] || npm ci; } && npm run smoke

k8s-up: ## Create a local kind cluster and deploy FlagForge to it
	scripts/k8s-local-up.sh

k8s-down: ## Delete the local kind cluster
	scripts/k8s-local-down.sh
