package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"sync"
	"time"

	"github.com/gorilla/websocket"
	"github.com/rs/cors"
)

// SlskDown Go Backend - Microservicios de alto rendimiento
type SearchRequest struct {
	Query      string            `json:"query"`
	Filters    map[string]string `json:"filters"`
	MaxResults int               `json:"max_results"`
}

type SearchResult struct {
	Username string `json:"username"`
	Filename string `json:"filename"`
	Size     int64  `json:"size"`
	Bitrate  string `json:"bitrate"`
	Country  string `json:"country"`
}

type SearchEngine struct {
	cache    map[string][]SearchResult
	cacheMu  sync.RWMutex
	rateLim  chan struct{}
	sem      chan struct{}
}

func NewSearchEngine() *SearchEngine {
	return &SearchEngine{
		cache:   make(map[string][]SearchResult),
		rateLim: make(chan struct{}, 100), // 100 requests/seg
		sem:     make(chan struct{}, 10),  // 10 concurrentes
	}
}

// Búsqueda paralela con goroutines
func (se *SearchEngine) Search(ctx context.Context, req SearchRequest) ([]SearchResult, error) {
	// Rate limiting
	select {
	case se.rateLim <- struct{}{}:
	default:
		return nil, fmt.Errorf("rate limit exceeded")
	}
	defer func() { <-se.rateLim }()

	// Cache check
	se.cacheMu.RLock()
	if cached, exists := se.cache[req.Query]; exists {
		se.cacheMu.RUnlock()
		return cached, nil
	}
	se.cacheMu.RUnlock()

	// Búsqueda simulada con concurrencia
	select {
	case se.sem <- struct{}{}:
	case <-ctx.Done():
		return nil, ctx.Err()
	}
	defer func() { <-se.sem }()

	// Simular búsqueda paralela
	results := make([]SearchResult, req.MaxResults)
	for i := 0; i < req.MaxResults; i++ {
		results[i] = SearchResult{
			Username: fmt.Sprintf("user_go_%d", i),
			Filename: fmt.Sprintf("%s_%d.mp3", req.Query, i),
			Size:     5242880,
			Bitrate:  "320",
			Country:  "US",
		}
	}

	// Guardar en cache
	se.cacheMu.Lock()
	se.cache[req.Query] = results
	se.cacheMu.Unlock()

	return results, nil
}

// WebSocket para streaming en tiempo real
var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		return true
	},
}

func (se *SearchEngine) HandleWebSocket(w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("WebSocket upgrade error: %v", err)
		return
	}
	defer conn.Close()

	// Streaming de actualizaciones
	ticker := time.NewTicker(1 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			update := map[string]interface{}{
				"type":        "status_update",
				"cache_size":  len(se.cache),
				"rate_limit":  len(se.rateLim),
				"concurrent":  len(se.sem),
				"timestamp":   time.Now().Unix(),
			}
			
			if err := conn.WriteJSON(update); err != nil {
				return
			}
		}
	}
}

func main() {
	engine := NewSearchEngine()

	// Setup CORS
	c := cors.New(cors.Options{
		AllowedOrigins: []string{"*"},
		AllowedMethods: []string{"GET", "POST", "PUT", "DELETE", "OPTIONS"},
		AllowedHeaders: []string{"*"},
	})

	mux := http.NewServeMux()

	// API endpoints
	mux.HandleFunc("/api/search", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
			return
		}

		var req SearchRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}

		results, err := engine.Search(r.Context(), req)
		if err != nil {
			http.Error(w, err.Error(), http.StatusInternalServerError)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]interface{}{
			"query":   req.Query,
			"results": results,
			"total":   len(results),
		})
	})

	// WebSocket endpoint
	mux.HandleFunc("/ws", engine.HandleWebSocket)

	// Health check
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]string{
			"status":    "ok",
			"service":   "slskdown-go",
			"version":   "1.0.0",
			"timestamp": time.Now().Format(time.RFC3339),
		})
	})

	// Start server
	handler := c.Handler(mux)
	port := ":8080"
	log.Printf("🚀 SlskDown Go API starting on %s", port)
	log.Fatal(http.ListenAndServe(port, handler))
}
