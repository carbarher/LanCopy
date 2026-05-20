#![windows_subsystem = "windows"]

use eframe::egui;
use egui::{Color32, Pos2, Stroke, Vec2};
use std::sync::{Arc, Mutex};
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::thread;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};
use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;

// ── RNG (xorshift64, no-dep) ─────────────────────────────────────────────────

fn rng_seed() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| {
            let nanos = d.subsec_nanos() as u64;
            let secs = d.as_secs();
            nanos ^ secs.wrapping_mul(6364136223846793005)
        })
        .unwrap_or(987654321)
}

fn xor64(s: &mut u64) -> u64 {
    *s ^= *s << 13;
    *s ^= *s >> 7;
    *s ^= *s << 17;
    *s
}

const N: usize = 15;
const WIN: usize = 5;
const BIG: i64 = 10_000_000_000; // >> max possible eval_board value (~1B)

type Board = [[i8; N]; N];

const LEARN_FILE: &str = "gomoku_learned.tsv";
const PREFS_FILE: &str = "gomoku_prefs.tsv";
const RANK_FILE: &str = "gomoku_ranking.tsv";

fn learning_file_path() -> PathBuf {
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|d| d.join(LEARN_FILE)))
        .unwrap_or_else(|| PathBuf::from(LEARN_FILE))
}

fn prefs_file_path() -> PathBuf {
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|d| d.join(PREFS_FILE)))
        .unwrap_or_else(|| PathBuf::from(PREFS_FILE))
}

fn rank_file_path() -> PathBuf {
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|d| d.join(RANK_FILE)))
        .unwrap_or_else(|| PathBuf::from(RANK_FILE))
}

#[derive(Clone, Copy)]
struct PersistedPrefs {
    depth_black: u32,
    depth_white: u32,
    profile_black: AiProfile,
    profile_white: AiProfile,
    mcts_threads: u8, // 0 = Auto
}

impl Default for PersistedPrefs {
    fn default() -> Self {
        Self {
            depth_black: 3,
            depth_white: 3,
            profile_black: AiProfile::Minimax,
            profile_white: AiProfile::Mcts,
            mcts_threads: 0,
        }
    }
}

fn load_prefs() -> PersistedPrefs {
    let mut p = PersistedPrefs::default();
    let Ok(txt) = fs::read_to_string(prefs_file_path()) else {
        return p;
    };
    for line in txt.lines() {
        let mut parts = line.split('\t');
        let (Some(k), Some(v)) = (parts.next(), parts.next()) else { continue; };
        match k {
            "depth_black" => if let Ok(x) = v.parse::<u32>() { p.depth_black = x.clamp(1, 5); },
            "depth_white" => if let Ok(x) = v.parse::<u32>() { p.depth_white = x.clamp(1, 5); },
            "profile_black" => if let Some(x) = AiProfile::from_key(v) { p.profile_black = x; },
            "profile_white" => if let Some(x) = AiProfile::from_key(v) { p.profile_white = x; },
            "mcts_threads" => if let Ok(x) = v.parse::<u8>() {
                p.mcts_threads = match x { 0 | 1 | 2 | 4 | 8 => x, _ => 0 };
            },
            _ => {}
        }
    }
    p
}

fn save_prefs(p: &PersistedPrefs) {
    let tmp = prefs_file_path().with_extension("tmp");
    let mut out = String::new();
    out.push_str(&format!("depth_black\t{}\n", p.depth_black));
    out.push_str(&format!("depth_white\t{}\n", p.depth_white));
    out.push_str(&format!("profile_black\t{}\n", p.profile_black.key()));
    out.push_str(&format!("profile_white\t{}\n", p.profile_white.key()));
    out.push_str(&format!("mcts_threads\t{}\n", p.mcts_threads));
    if fs::write(&tmp, out).is_ok() {
        let _ = fs::rename(&tmp, prefs_file_path());
    }
}

#[derive(Clone, Copy, Default)]
struct RankEntry {
    wins: u32,
    losses: u32,
    draws: u32,
}

impl RankEntry {
    fn games(&self) -> u32 { self.wins + self.losses + self.draws }
    fn points(&self) -> f32 { self.wins as f32 + 0.5 * self.draws as f32 }
}

fn load_rankings() -> HashMap<AiKind, RankEntry> {
    let mut out: HashMap<AiKind, RankEntry> = HashMap::new();
    for k in [
        AiKind::Minimax,
        AiKind::Mcts,
        AiKind::Greedy,
        AiKind::Pvs,
        AiKind::MctsRave,
        AiKind::Book,
        AiKind::Policy,
        AiKind::Hybrid,
    ] {
        out.insert(k, RankEntry::default());
    }
    let Ok(txt) = fs::read_to_string(rank_file_path()) else {
        return out;
    };
    for line in txt.lines() {
        if line.is_empty() || line.starts_with('#') { continue; }
        let mut parts = line.split('\t');
        let (Some(ks), Some(ws), Some(ls), Some(ds)) =
            (parts.next(), parts.next(), parts.next(), parts.next()) else { continue; };
        let Some(kind) = AiKind::from_key(ks) else { continue; };
        let (Ok(w), Ok(l), Ok(d)) = (ws.parse::<u32>(), ls.parse::<u32>(), ds.parse::<u32>()) else { continue; };
        out.insert(kind, RankEntry { wins: w, losses: l, draws: d });
    }
    out
}

fn save_rankings(rankings: &HashMap<AiKind, RankEntry>) {
    let tmp = rank_file_path().with_extension("tmp");
    let mut out = String::new();
    out.push_str("# kind\twins\tlosses\tdraws\n");
    for kind in [
        AiKind::Minimax,
        AiKind::Mcts,
        AiKind::Greedy,
        AiKind::Pvs,
        AiKind::MctsRave,
        AiKind::Book,
        AiKind::Policy,
        AiKind::Hybrid,
    ] {
        let r = rankings.get(&kind).copied().unwrap_or_default();
        out.push_str(&format!("{}\t{}\t{}\t{}\n", kind.key(), r.wins, r.losses, r.draws));
    }
    if fs::write(&tmp, out).is_ok() {
        let _ = fs::rename(&tmp, rank_file_path());
    }
}

fn mcts_worker_count(pref: u8) -> usize {
    let auto = std::thread::available_parallelism()
        .map(|n| n.get())
        .unwrap_or(1)
        .clamp(1, 8);
    match pref {
        0 => auto,
        1 | 2 | 4 | 8 => (pref as usize).min(auto),
        _ => auto,
    }
}

fn load_learned_map() -> HashMap<u64, (f32, u32)> {
    let path = learning_file_path();
    let Ok(txt) = fs::read_to_string(path) else {
        return HashMap::new();
    };
    let mut out = HashMap::new();
    for line in txt.lines() {
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        let mut parts = line.split('\t');
        let (Some(hs), Some(ts), Some(cs)) = (parts.next(), parts.next(), parts.next()) else {
            continue;
        };
        let (Ok(h), Ok(total), Ok(cnt)) = (
            hs.parse::<u64>(),
            ts.parse::<f32>(),
            cs.parse::<u32>(),
        ) else {
            continue;
        };
        out.insert(h, (total, cnt));
    }
    out
}

fn save_learned_map(map: &HashMap<u64, (f32, u32)>) {
    let path = learning_file_path();
    let tmp = path.with_extension("tmp");
    let mut lines = String::with_capacity(map.len().saturating_mul(32));
    lines.push_str("# hash\ttotal\tcount\n");
    for (h, (total, cnt)) in map {
        lines.push_str(&format!("{}\t{}\t{}\n", h, total, cnt));
    }
    if fs::write(&tmp, lines).is_ok() {
        let _ = fs::rename(&tmp, &path);
    }
}

// ── Transposition table (Zobrist hashing) ────────────────────────────────────

const TT_EXACT: u8 = 0;
const TT_LOWER: u8 = 1; // fail-high (lower bound)
const TT_UPPER: u8 = 2; // fail-low  (upper bound)
const TT_SIZE:  usize = 1 << 18; // 262 144 entries ≈ 6 MB per search

#[derive(Clone, Copy)]
struct TtEntry {
    hash:      u64,
    score:     i64,
    best_move: (u8, u8), // best move (row,col) found; (255,255) = none
    depth:     u8,
    flag:      u8,
}

static SEARCH_TT: std::sync::OnceLock<Mutex<Vec<TtEntry>>> = std::sync::OnceLock::new();
static SEARCH_EVAL_CACHE: std::sync::OnceLock<Mutex<HashMap<u64, i64>>> = std::sync::OnceLock::new();
static MCTS_HINTS: std::sync::OnceLock<Mutex<HashMap<(u64, i8), (u8, u8)>>> = std::sync::OnceLock::new();
static BENCH_DISABLE_OPT: AtomicBool = AtomicBool::new(false);
const SEARCH_EVAL_CACHE_MAX: usize = 120_000;
const MCTS_HINTS_MAX: usize = 40_000;

static ZOBRIST: std::sync::OnceLock<[[u64; 2]; N * N]> = std::sync::OnceLock::new();

fn init_zobrist() -> [[u64; 2]; N * N] {
    let mut s = 0xdead_beef_cafe_babe_u64;
    let mut t = [[0u64; 2]; N * N];
    for row in t.iter_mut() {
        for v in row.iter_mut() { *v = xor64(&mut s); }
    }
    t
}

fn zobrist_hash(board: &Board, zt: &[[u64; 2]; N * N]) -> u64 {
    let mut h = 0u64;
    for r in 0..N {
        for c in 0..N {
            let v = board[r][c];
            if v != 0 {
                h ^= zt[r * N + c][(v - 1) as usize];
            }
        }
    }
    h
}

#[derive(Clone, Copy, PartialEq)]
enum Mode {
    HumanAI,
    AiAi,
    Auto, // continuous AI vs AI, auto-restart
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
enum AiKind { Minimax, Mcts, Greedy, Pvs, MctsRave, Book, Policy, Hybrid }

impl AiKind {
    fn key(self) -> &'static str {
        match self {
            AiKind::Minimax => "minimax",
            AiKind::Mcts => "mcts",
            AiKind::Greedy => "greedy",
            AiKind::Pvs => "pvs",
            AiKind::MctsRave => "mcts_rave",
            AiKind::Book => "book",
            AiKind::Policy => "policy",
            AiKind::Hybrid => "hybrid",
        }
    }

    fn label(self) -> &'static str {
        match self {
            AiKind::Minimax => "Minimax",
            AiKind::Mcts => "MCTS",
            AiKind::Greedy => "Greedy",
            AiKind::Pvs => "PVS",
            AiKind::MctsRave => "MCTS-RAVE",
            AiKind::Book => "Book",
            AiKind::Policy => "Policy",
            AiKind::Hybrid => "Hybrid",
        }
    }

    fn from_key(s: &str) -> Option<Self> {
        match s {
            "minimax" => Some(AiKind::Minimax),
            "mcts" => Some(AiKind::Mcts),
            "greedy" => Some(AiKind::Greedy),
            "pvs" => Some(AiKind::Pvs),
            "mcts_rave" => Some(AiKind::MctsRave),
            "book" => Some(AiKind::Book),
            "policy" => Some(AiKind::Policy),
            "hybrid" => Some(AiKind::Hybrid),
            _ => None,
        }
    }
}

#[derive(Clone, Copy, PartialEq)]
enum AiProfile { Minimax, Mcts, Greedy, Pvs, MctsRave, Book, Policy, Hybrid, Random }

impl AiProfile {
    fn key(self) -> &'static str {
        match self {
            AiProfile::Minimax => "minimax",
            AiProfile::Mcts => "mcts",
            AiProfile::Greedy => "greedy",
            AiProfile::Pvs => "pvs",
            AiProfile::MctsRave => "mcts_rave",
            AiProfile::Book => "book",
            AiProfile::Policy => "policy",
            AiProfile::Hybrid => "hybrid",
            AiProfile::Random => "random",
        }
    }

    fn label(self) -> &'static str {
        match self {
            AiProfile::Minimax => "Minimax",
            AiProfile::Mcts => "MCTS",
            AiProfile::Greedy => "Greedy",
            AiProfile::Pvs => "PVS",
            AiProfile::MctsRave => "MCTS-RAVE",
            AiProfile::Book => "Book",
            AiProfile::Policy => "Policy",
            AiProfile::Hybrid => "Hybrid",
            AiProfile::Random => "Random partida",
        }
    }

    fn from_key(s: &str) -> Option<Self> {
        match s {
            "minimax" => Some(AiProfile::Minimax),
            "mcts" => Some(AiProfile::Mcts),
            "greedy" => Some(AiProfile::Greedy),
            "pvs" => Some(AiProfile::Pvs),
            "mcts_rave" => Some(AiProfile::MctsRave),
            "book" => Some(AiProfile::Book),
            "policy" => Some(AiProfile::Policy),
            "hybrid" => Some(AiProfile::Hybrid),
            "random" => Some(AiProfile::Random),
            _ => None,
        }
    }
}

/// Arena node for MCTS.
struct MctsNode {
    visits:     u32,
    value:      f32,       // total outcome from mcts_player perspective
    mv:         (u8, u8),  // 255,255 = root sentinel
    parent:     u32,       // u32::MAX = no parent
    children:   Vec<u32>,
    untried:    Vec<(u8,u8)>,
    whose_turn: i8,        // player to move at this node
}

#[derive(Default)]
struct Stats {
    wins_black: u32,
    wins_white: u32,
    draws: u32,
    total: u32,
}

struct App {
    board: Board,
    turn: i8,
    over: bool,
    winner: Option<i8>,
    draw: bool,
    mode: Mode,
    last_mv: Option<(usize, usize)>,
    ai_busy: Arc<AtomicBool>,
    ai_result: Arc<Mutex<Option<(usize, usize)>>>,
    gid: u64,
    shared_gid: Arc<AtomicU64>,
    stats: Stats,
    auto_delay: Option<Instant>,
    pieces: u32,
    winning_cells: Vec<(usize, usize)>,
    // New features
    ai_depth_black: u32,     // minimax depth for black (1-5)
    ai_depth_white: u32,     // minimax depth for white (1-5)
    paused: bool,            // Auto mode pause
    history: Vec<(Board, i8, Option<(usize, usize)>, u32, u64, usize)>, // undo snapshots
    streak_black: u32,
    streak_white: u32,
    auto_start: Option<Instant>, // for games/min
    // Learning AI
    learned: Arc<Mutex<HashMap<u64, (f32, u32)>>>,
    ai_profile_black: AiProfile,
    ai_profile_white: AiProfile,
    mcts_threads_pref: u8, // 0=Auto, 1/2/4/8 fijo
    game_kind_black: AiKind,
    game_kind_white: AiKind,
    rankings: HashMap<AiKind, RankEntry>,
    mcts_sims: Arc<AtomicU64>,   // last MCTS simulation count (for display)
    board_hash: u64,             // incremental Zobrist hash of current board
    game_positions: Vec<u64>,   // hashes after each move (for TD learning)
}

impl App {
    fn new() -> Self {
        let learned_map = load_learned_map();
        let prefs = load_prefs();
        let rankings = load_rankings();
        let mut app = Self {
            board: [[0; N]; N],
            turn: 1,
            over: false,
            winner: None,
            draw: false,
            mode: Mode::HumanAI,
            last_mv: None,
            ai_busy: Arc::new(AtomicBool::new(false)),
            ai_result: Arc::new(Mutex::new(None)),
            gid: 0,
            shared_gid: Arc::new(AtomicU64::new(0)),
            stats: Stats::default(),
            auto_delay: None,
            pieces: 0,
            winning_cells: Vec::new(),
            ai_depth_black: prefs.depth_black,
            ai_depth_white: prefs.depth_white,
            paused: false,
            history: Vec::new(),
            streak_black: 0,
            streak_white: 0,
            auto_start: None,
            learned: Arc::new(Mutex::new(learned_map)),
            ai_profile_black: prefs.profile_black,
            ai_profile_white: prefs.profile_white,
            mcts_threads_pref: prefs.mcts_threads,
            game_kind_black: AiKind::Minimax,
            game_kind_white: AiKind::Mcts,
            rankings,
            mcts_sims: Arc::new(AtomicU64::new(0)),
            board_hash: 0,
            game_positions: Vec::new(),
        };
        app.pick_game_ai_kinds();
        app
    }

    fn persist_learning(&self) {
        if let Ok(l) = self.learned.try_lock() {
            save_learned_map(&l);
        }
    }

    fn persist_prefs(&self) {
        save_prefs(&PersistedPrefs {
            depth_black: self.ai_depth_black,
            depth_white: self.ai_depth_white,
            profile_black: self.ai_profile_black,
            profile_white: self.ai_profile_white,
            mcts_threads: self.mcts_threads_pref,
        });
    }

    fn persist_rankings(&self) {
        save_rankings(&self.rankings);
    }

    fn pick_kind_for_profile(profile: AiProfile, seed: &mut u64) -> AiKind {
        match profile {
            AiProfile::Minimax => AiKind::Minimax,
            AiProfile::Mcts => AiKind::Mcts,
            AiProfile::Greedy => AiKind::Greedy,
            AiProfile::Pvs => AiKind::Pvs,
            AiProfile::MctsRave => AiKind::MctsRave,
            AiProfile::Book => AiKind::Book,
            AiProfile::Policy => AiKind::Policy,
            AiProfile::Hybrid => AiKind::Hybrid,
            AiProfile::Random => {
                match xor64(seed) % 8 {
                    0 => AiKind::Minimax,
                    1 => AiKind::Mcts,
                    2 => AiKind::Greedy,
                    3 => AiKind::Pvs,
                    4 => AiKind::MctsRave,
                    5 => AiKind::Book,
                    6 => AiKind::Policy,
                    _ => AiKind::Hybrid,
                }
            }
        }
    }

    fn pick_game_ai_kinds(&mut self) {
        let mut s = rng_seed() ^ self.gid.wrapping_mul(11400714819323198485);
        self.game_kind_black = Self::pick_kind_for_profile(self.ai_profile_black, &mut s);
        self.game_kind_white = Self::pick_kind_for_profile(self.ai_profile_white, &mut s);
    }

    fn update_ranking_after_game(&mut self) {
        let bk = self.game_kind_black;
        let wk = self.game_kind_white;
        if self.draw {
            self.rankings.entry(bk).or_default().draws += 1;
            self.rankings.entry(wk).or_default().draws += 1;
        } else if self.winner == Some(1) {
            self.rankings.entry(bk).or_default().wins += 1;
            self.rankings.entry(wk).or_default().losses += 1;
        } else if self.winner == Some(2) {
            self.rankings.entry(wk).or_default().wins += 1;
            self.rankings.entry(bk).or_default().losses += 1;
        }
        self.persist_rankings();
    }

    fn reset(&mut self) {
        self.pick_game_ai_kinds();
        self.board = [[0; N]; N];
        self.turn = 1;
        self.over = false;
        self.winner = None;
        self.draw = false;
        self.last_mv = None;
        self.auto_delay = None;
        self.pieces = 0;
        self.winning_cells.clear();
        self.history.clear();
        self.board_hash = 0;
        self.game_positions.clear();
        self.gid += 1;
        self.shared_gid.store(self.gid, Ordering::SeqCst);
        self.ai_busy.store(false, Ordering::SeqCst);
        *self.ai_result.lock().unwrap() = None;
    }

    fn reset_stats(&mut self) {
        self.stats = Stats::default();
        self.streak_black = 0;
        self.streak_white = 0;
        self.auto_start = None;
    }

    fn reset_rankings(&mut self) {
        self.rankings.clear();
        for k in [
            AiKind::Minimax,
            AiKind::Mcts,
            AiKind::Greedy,
            AiKind::Pvs,
            AiKind::MctsRave,
            AiKind::Book,
            AiKind::Policy,
            AiKind::Hybrid,
        ] {
            self.rankings.insert(k, RankEntry::default());
        }
        self.persist_rankings();
    }

    fn save_snapshot(&mut self) {
        self.history.push((self.board, self.turn, self.last_mv, self.pieces,
                           self.board_hash, self.game_positions.len()));
    }

    fn undo(&mut self) {
        // Pop 1 snapshot (state before last human move) and restore it
        let Some((board, turn, last_mv, pieces, board_hash, gp_len)) = self.history.pop() else { return; };
        self.board = board;
        self.turn = turn;
        self.last_mv = last_mv;
        self.pieces = pieces;
        self.board_hash = board_hash;
        self.game_positions.truncate(gp_len);
        self.over = false;
        self.winner = None;
        self.draw = false;
        self.winning_cells.clear();
        self.gid += 1;
        self.shared_gid.store(self.gid, Ordering::SeqCst);
        self.ai_busy.store(false, Ordering::SeqCst);
        *self.ai_result.lock().unwrap() = None;
    }

    /// Called when a game just ended (win or draw) — runs TD learning update.
    fn run_learning(&mut self, winner: i8) {
        let positions = self.game_positions.clone();
        for player in [1i8, 2i8] {
            let kind = if player == 1 { self.game_kind_black } else { self.game_kind_white };
            if kind == AiKind::Mcts || kind == AiKind::MctsRave || kind == AiKind::Hybrid {
                if let Ok(mut l) = self.learned.try_lock() {
                    td_update(&positions, winner, player, &mut l);
                }
            }
        }
        self.persist_learning();
    }

    fn place(&mut self, r: usize, c: usize) {
        let zt = ZOBRIST.get_or_init(init_zobrist);
        self.board[r][c] = self.turn;
        self.board_hash ^= zt[r * N + c][(self.turn - 1) as usize];
        self.game_positions.push(self.board_hash);
        self.last_mv = Some((r, c));
        self.pieces += 1;
        if check_win(&self.board, r, c, self.turn) {
            self.over = true;
            self.winner = Some(self.turn);
            self.winning_cells = find_win_cells(&self.board, r, c, self.turn);
            match self.turn {
                1 => { self.stats.wins_black += 1; self.streak_black += 1; self.streak_white = 0; }
                _ => { self.stats.wins_white += 1; self.streak_white += 1; self.streak_black = 0; }
            }
            self.stats.total += 1;
            let winner = self.turn;
            self.run_learning(winner);
            self.update_ranking_after_game();
        } else if self.pieces >= (N * N) as u32 {
            self.over = true;
            self.draw = true;
            self.stats.draws += 1;
            self.stats.total += 1;
            self.streak_black = 0;
            self.streak_white = 0;
            self.run_learning(0);
            self.update_ranking_after_game();
        } else {
            self.turn = 3 - self.turn;
        }
    }

    fn launch_ai(&mut self, ctx: egui::Context) {
        self.ai_busy.store(true, Ordering::SeqCst);
        let board = self.board;
        let player = self.turn;
        let result = Arc::clone(&self.ai_result);
        let busy = Arc::clone(&self.ai_busy);
        let shared_gid = Arc::clone(&self.shared_gid);
        let my_gid = self.gid;
        let seed = rng_seed() ^ my_gid.wrapping_mul(2654435761);
        let depth = if player == 1 { self.ai_depth_black } else { self.ai_depth_white };
        let ai_kind = if player == 1 { self.game_kind_black } else { self.game_kind_white };
        let mcts_threads_pref = self.mcts_threads_pref;
        let learned = Arc::clone(&self.learned);
        let mcts_sims = Arc::clone(&self.mcts_sims);
        let base_hash = self.board_hash;
        let n_sims: u32 = if self.mode == Mode::Auto { 1500 } else { 3000 };
        let delay_ms: u64 = match self.mode {
            Mode::Auto => 0,
            _ => 50,
        };

        thread::spawn(move || {
            if delay_ms > 0 { thread::sleep(Duration::from_millis(delay_ms)); }
            if shared_gid.load(Ordering::SeqCst) != my_gid {
                busy.store(false, Ordering::SeqCst);
                ctx.request_repaint();
                return;
            }
            let mv = match ai_kind {
                AiKind::Minimax => ai_best_move(&board, player, depth, &shared_gid, my_gid, seed),
                AiKind::Mcts => {
                    let zt = ZOBRIST.get_or_init(init_zobrist);
                    let workers = mcts_worker_count(mcts_threads_pref);
                    let (m, sims) = if workers <= 1 || n_sims < 1200 {
                        // Fast path: avoid cloning learned map when running single-thread MCTS.
                        let snap = learned.lock().unwrap();
                        mcts_best_move(&board, player, n_sims, &snap, zt,
                                       base_hash, &shared_gid, my_gid, seed)
                    } else {
                        let snap = Arc::new(learned.lock().unwrap().clone());
                        mcts_best_move_parallel(&board, player, n_sims, snap, zt,
                                                base_hash, &shared_gid, my_gid, seed, workers)
                    };
                    mcts_sims.store(sims as u64, Ordering::Relaxed);
                    m
                },
                AiKind::Greedy => {
                    mcts_sims.store(0, Ordering::Relaxed);
                    ai_greedy_move(&board, player, seed)
                },
                AiKind::Pvs => {
                    mcts_sims.store(0, Ordering::Relaxed);
                    let d = (depth + 1).min(6);
                    ai_best_move(&board, player, d, &shared_gid, my_gid, seed)
                },
                AiKind::MctsRave => {
                    let zt = ZOBRIST.get_or_init(init_zobrist);
                    let workers = mcts_worker_count(mcts_threads_pref);
                    let sims = n_sims.saturating_mul(3) / 2;
                    let (m, sims_done) = if workers <= 1 || sims < 1200 {
                        let snap = learned.lock().unwrap();
                        mcts_best_move(&board, player, sims, &snap, zt,
                                       base_hash, &shared_gid, my_gid, seed ^ 0xA11CE)
                    } else {
                        let snap = Arc::new(learned.lock().unwrap().clone());
                        mcts_best_move_parallel(&board, player, sims, snap, zt,
                                                base_hash, &shared_gid, my_gid, seed ^ 0xA11CE, workers)
                    };
                    mcts_sims.store(sims_done as u64, Ordering::Relaxed);
                    m
                },
                AiKind::Book => {
                    mcts_sims.store(0, Ordering::Relaxed);
                    ai_book_move(&board, player, seed)
                },
                AiKind::Policy => {
                    mcts_sims.store(0, Ordering::Relaxed);
                    ai_policy_move(&board, player, seed)
                },
                AiKind::Hybrid => {
                    let pieces = board.iter().flatten().filter(|&&v| v != 0).count();
                    if pieces <= 8 {
                        mcts_sims.store(0, Ordering::Relaxed);
                        ai_book_move(&board, player, seed)
                    } else if pieces <= 40 {
                        let zt = ZOBRIST.get_or_init(init_zobrist);
                        let workers = mcts_worker_count(mcts_threads_pref);
                        let sims = n_sims;
                        let (m, sims_done) = if workers <= 1 || sims < 1200 {
                            let snap = learned.lock().unwrap();
                            mcts_best_move(&board, player, sims, &snap, zt,
                                           base_hash, &shared_gid, my_gid, seed ^ 0xBEEFu64)
                        } else {
                            let snap = Arc::new(learned.lock().unwrap().clone());
                            mcts_best_move_parallel(&board, player, sims, snap, zt,
                                                    base_hash, &shared_gid, my_gid, seed ^ 0xBEEFu64, workers)
                        };
                        mcts_sims.store(sims_done as u64, Ordering::Relaxed);
                        m
                    } else {
                        mcts_sims.store(0, Ordering::Relaxed);
                        ai_best_move(&board, player, depth, &shared_gid, my_gid, seed)
                    }
                },
            };
            if shared_gid.load(Ordering::SeqCst) == my_gid {
                match mv {
                    Some(m) => { *result.lock().unwrap() = Some(m); }
                    None => { busy.store(false, Ordering::SeqCst); }
                }
            } else {
                busy.store(false, Ordering::SeqCst);
            }
            ctx.request_repaint();
        });
    }
}

// ── Game logic ───────────────────────────────────────────────────────────────

fn check_win(board: &Board, r: usize, c: usize, p: i8) -> bool {
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    for (dr, dc) in DIRS {
        let mut cnt = 1i32;
        for sign in [1i32, -1] {
            let (mut rr, mut cc) = (r as i32 + sign * dr, c as i32 + sign * dc);
            while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && board[rr as usize][cc as usize] == p
            {
                cnt += 1;
                rr += sign * dr;
                cc += sign * dc;
            }
        }
        if cnt >= WIN as i32 {
            return true;
        }
    }
    false
}

/// Longest run player `p` would make by placing at (r,c).
fn max_run_if_placed(board: &Board, r: usize, c: usize, p: i8) -> usize {
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    let mut b = *board;
    b[r][c] = p;
    let mut best = 0usize;
    for (dr, dc) in DIRS {
        let mut cnt = 1usize;
        for sign in [1i32, -1] {
            let (mut rr, mut cc) = (r as i32 + sign * dr, c as i32 + sign * dc);
            while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && b[rr as usize][cc as usize] == p
            {
                cnt += 1;
                rr += sign * dr;
                cc += sign * dc;
            }
        }
        if cnt > best { best = cnt; }
    }
    best
}

/// `atk_w` scales own windows; `def_w` scales opponent windows.
fn score_seg(seg: &[i8], p: i8, atk_w: i64, def_w: i64) -> i64 {
    let opp = 3 - p;
    let mut s = 0i64;
    if seg.len() < WIN {
        return 0;
    }
    for w in seg.windows(WIN) {
        let me = w.iter().filter(|&&x| x == p).count();
        let them = w.iter().filter(|&&x| x == opp).count();
        if them == 0 {
            s += atk_w * match me {
                5 => 10_000_000,
                4 => 100_000,
                3 => 1_000,
                2 => 100,
                _ => 10,
            };
        } else if me == 0 {
            s -= def_w * match them {
                5 => 10_000_000,
                4 => 100_000,
                3 => 1_000,
                2 => 100,
                _ => 10,
            };
        }
    }
    s
}

/// Returns the ≥5 cells that form the winning line for player `p` at (r,c).
fn find_win_cells(board: &Board, r: usize, c: usize, p: i8) -> Vec<(usize, usize)> {
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    for (dr, dc) in DIRS {
        let mut cells = vec![(r, c)];
        for sign in [1i32, -1] {
            let (mut rr, mut cc) = (r as i32 + sign * dr, c as i32 + sign * dc);
            while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && board[rr as usize][cc as usize] == p
            {
                cells.push((rr as usize, cc as usize));
                rr += sign * dr;
                cc += sign * dc;
            }
        }
        if cells.len() >= WIN {
            return cells;
        }
    }
    vec![]
}

fn eval_board(board: &Board, p: i8) -> i64 {
    // Black (1) = offensive: values own threats more.
    // White (2) = defensive: values blocking opponent more.
    let (atk_w, def_w): (i64, i64) = if p == 1 { (16, 8) } else { (8, 16) };
    let mut s = 0i64;

    // Rows — board[r] is already [i8; N], no alloc
    for r in 0..N {
        s += score_seg(&board[r], p, atk_w, def_w);
    }
    // Columns
    let mut buf = [0i8; N];
    for c in 0..N {
        for r in 0..N { buf[r] = board[r][c]; }
        s += score_seg(&buf, p, atk_w, def_w);
    }
    // Diagonals (reuse buf for each segment)
    for start in 0..N {
        let len = N - start;
        for i in 0..len { buf[i] = board[start + i][i]; }
        s += score_seg(&buf[..len], p, atk_w, def_w);
        if start > 0 {
            for i in 0..len { buf[i] = board[i][start + i]; }
            s += score_seg(&buf[..len], p, atk_w, def_w);
        }
        for i in 0..len { buf[i] = board[start + i][N - 1 - i]; }
        s += score_seg(&buf[..len], p, atk_w, def_w);
        if start > 0 {
            for i in 0..len { buf[i] = board[i][N - 1 - start - i]; }
            s += score_seg(&buf[..len], p, atk_w, def_w);
        }
    }
    s
}

fn candidates(board: &Board, radius: usize) -> Vec<(usize, usize)> {
    // Iterate occupied cells and mark their empty neighbours as candidates.
    // Much faster than scanning all empty cells for nearby stones.
    let mut seen = [[false; N]; N];
    let mut result = Vec::with_capacity(64);
    for r in 0..N {
        for c in 0..N {
            if board[r][c] == 0 { continue; }
            let r_lo = r.saturating_sub(radius);
            let r_hi = (r + radius + 1).min(N);
            let c_lo = c.saturating_sub(radius);
            let c_hi = (c + radius + 1).min(N);
            for rr in r_lo..r_hi {
                for cc in c_lo..c_hi {
                    if board[rr][cc] == 0 && !seen[rr][cc] {
                        seen[rr][cc] = true;
                        result.push((rr, cc));
                    }
                }
            }
        }
    }
    if result.is_empty() {
        result.push((N / 2, N / 2));
    }
    result
}

/// Score only the 4 lines through (r,c) — O(N) vs eval_board's O(N²).
/// Board must already have player `p` placed at (r,c).
fn quick_score(board: &Board, r: usize, c: usize, p: i8) -> i64 {
    let (atk_w, def_w): (i64, i64) = if p == 1 { (16, 8) } else { (8, 16) };
    let mut s = 0i64;
    let mut seg = [0i8; N * 2];
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    for (dr, dc) in DIRS {
        let (mut rr, mut cc) = (r as i32, c as i32);
        while rr - dr >= 0 && rr - dr < N as i32 && cc - dc >= 0 && cc - dc < N as i32 {
            rr -= dr; cc -= dc;
        }
        let mut len = 0usize;
        while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32 {
            seg[len] = board[rr as usize][cc as usize];
            len += 1;
            rr += dr; cc += dc;
        }
        if len >= WIN { s += score_seg(&seg[..len], p, atk_w, def_w); }
    }
    s
}

fn minimax(
    board: &mut Board,
    depth: u32,
    mut alpha: i64,
    mut beta: i64,
    maximizing: bool,
    ai: i8,
    shared_gid: &Arc<AtomicU64>,
    my_gid: u64,
    hash: u64,
    tt: &mut Vec<TtEntry>,
    eval_cache: &mut HashMap<u64, i64>,
    killers: &mut [[Option<(u8, u8)>; 2]; 8],
    ext: u32,          // extension count for this path (capped at 2)
    history: &mut [[i32; N]; N], // history heuristic: moves that caused cutoffs
) -> i64 {
    if shared_gid.load(Ordering::Relaxed) != my_gid { return 0; }

    // ── TT probe ─────────────────────────────────────────────────────────────
    let tt_idx     = (hash as usize) & (TT_SIZE - 1);
    let orig_alpha = alpha;
    let orig_beta  = beta;
    let mut tt_move: Option<(usize, usize)> = None;
    {
        let e = tt[tt_idx];
        if e.hash == hash {
            // Hash move usable for ordering regardless of depth
            let (br, bc) = (e.best_move.0 as usize, e.best_move.1 as usize);
            if br < N && bc < N { tt_move = Some((br, bc)); }

            if (e.depth as u32) >= depth {
                if e.flag == TT_EXACT { return e.score; }
                if e.flag == TT_LOWER && e.score > alpha { alpha = e.score; }
                if e.flag == TT_UPPER && e.score < beta  { beta  = e.score; }
                if alpha >= beta { return e.score; }
            }
        }
    }

    if depth == 0 {
        let key = hash ^ ((ai as u64) << 61);
        let sc = if let Some(v) = eval_cache.get(&key) {
            *v
        } else {
            let v = eval_board(board, ai);
            if eval_cache.len() < SEARCH_EVAL_CACHE_MAX {
                eval_cache.insert(key, v);
            }
            v
        };
        tt[tt_idx] = TtEntry { hash, score: sc, best_move: (255, 255), depth: 0, flag: TT_EXACT };
        return sc;
    }

    let zt     = ZOBRIST.get_or_init(init_zobrist);
    let player = if maximizing { ai } else { 3 - ai };
    let cands  = candidates(board, 2);
    let kid    = depth.min(7) as usize; // killer slot index

    // ── Move ordering: TT hash move > wins > killers > quick_score ────────────
    let mut scored: Vec<(i64, (usize, usize))> = cands
        .iter()
        .map(|&(r, c)| {
            board[r][c] = player;
            let mut s = if check_win(board, r, c, player) {
                if maximizing { BIG * 2 } else { -BIG * 2 }
            } else {
                // Always score from AI's (maximizer's) view.
                // Maximizing: player == ai, no change.
                // Minimizing: opponent's piece placed, scored from ai's perspective
                //   → low values = bad for ai = good for minimizer → correct ascending order.
                quick_score(board, r, c, ai)
            };
            board[r][c] = 0;
            // TT hash move: just below win so the skip-recursion check still works
            if tt_move == Some((r, c)) && s.abs() < BIG {
                s = if maximizing { BIG - 1 } else { -(BIG - 1) };
            }
            // Killer moves: secondary boost; history: tertiary
            else if s.abs() < BIG / 2 {
                let m = (r as u8, c as u8);
                if killers[kid][0] == Some(m) || killers[kid][1] == Some(m) {
                    s = if maximizing { BIG / 4 } else { -(BIG / 4) };
                } else {
                    let h = (history[r][c] as i64).clamp(0, BIG / 8);
                    s += if maximizing { h } else { -h };
                }
            }
            (s, (r, c))
        })
        .collect();

    if maximizing { scored.sort_unstable_by(|a, b| b.0.cmp(&a.0)); }
    else          { scored.sort_unstable_by(|a, b| a.0.cmp(&b.0)); }
    let scored: Vec<_> = scored.into_iter().take(12).collect();

    let mut best     = if maximizing { -BIG } else { BIG };
    let mut best_pos = (255u8, 255u8);

    if maximizing {
        for (ord, (r, c)) in scored {
            board[r][c] = player;
            let child_hash = hash ^ zt[r * N + c][(player - 1) as usize];
            let sc = if ord >= BIG {
                BIG + depth as i64 // ordering confirmed a win, skip recursion
            } else {
                // Threat extension: run-of-4 attack detected → +1 extra ply, max 2
                let (nd, ne) = if ext < 2 && ord > 500_000 {
                    (depth, ext + 1)
                } else {
                    (depth - 1, ext)
                };
                minimax(board, nd, alpha, beta, false, ai,
                    shared_gid, my_gid, child_hash, tt, eval_cache, killers, ne, history)
            };
            board[r][c] = 0;
            if sc > best { best = sc; best_pos = (r as u8, c as u8); }
            if best > alpha { alpha = best; }
            if beta <= alpha {
                let m = (r as u8, c as u8);
                if killers[kid][0] != Some(m) {
                    killers[kid][1] = killers[kid][0];
                    killers[kid][0] = Some(m);
                }
                history[r][c] = history[r][c].saturating_add(1 << depth.min(8));
                break;
            }
        }
    } else {
        for (ord, (r, c)) in scored {
            board[r][c] = player;
            let child_hash = hash ^ zt[r * N + c][(player - 1) as usize];
            let sc = if ord <= -BIG {
                -(BIG + depth as i64)
            } else {
                let (nd, ne) = if ext < 2 && ord < -500_000 {
                    (depth, ext + 1)
                } else {
                    (depth - 1, ext)
                };
                minimax(board, nd, alpha, beta, true, ai,
                    shared_gid, my_gid, child_hash, tt, eval_cache, killers, ne, history)
            };
            board[r][c] = 0;
            if sc < best { best = sc; best_pos = (r as u8, c as u8); }
            if best < beta { beta = best; }
            if beta <= alpha {
                let m = (r as u8, c as u8);
                if killers[kid][0] != Some(m) {
                    killers[kid][1] = killers[kid][0];
                    killers[kid][0] = Some(m);
                }
                history[r][c] = history[r][c].saturating_add(1 << depth.min(8));
                break;
            }
        }
    }

    // ── TT store ─────────────────────────────────────────────────────────────
    let flag = if best <= orig_alpha { TT_UPPER }
               else if best >= orig_beta  { TT_LOWER }
               else                  { TT_EXACT };
    if tt[tt_idx].depth <= depth as u8 {
        tt[tt_idx] = TtEntry { hash, score: best, best_move: best_pos,
                               depth: depth as u8, flag };
    }

    best
}

/// Count how many directions have a run >= min_run after placing p at (r,c).
#[allow(dead_code)]
fn fork_threat_count(board: &Board, r: usize, c: usize, p: i8, min_run: usize) -> usize {
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    let mut b = *board;
    b[r][c] = p;
    let mut count = 0usize;
    for (dr, dc) in DIRS {
        let mut run = 1usize;
        for sign in [1i32, -1] {
            let (mut rr, mut cc) = (r as i32 + sign * dr, c as i32 + sign * dc);
            while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && b[rr as usize][cc as usize] == p
            {
                run += 1;
                rr += sign * dr;
                cc += sign * dc;
            }
        }
        if run >= min_run { count += 1; }
    }
    count
}

/// Like fork_threat_count but only counts directions that have at least one open end.
/// Prevents false-positive forks on fully-blocked (dead) threats.
fn open_fork_count(board: &Board, r: usize, c: usize, p: i8, min_run: usize) -> usize {
    const DIRS: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];
    let mut b = *board;
    b[r][c] = p;
    let mut count = 0usize;
    for (dr, dc) in DIRS {
        let mut run = 1usize;
        let mut open = 0usize;
        for sign in [1i32, -1] {
            let (mut rr, mut cc) = (r as i32 + sign * dr, c as i32 + sign * dc);
            while rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && b[rr as usize][cc as usize] == p
            {
                run += 1;
                rr += sign * dr;
                cc += sign * dc;
            }
            if rr >= 0 && rr < N as i32 && cc >= 0 && cc < N as i32
                && b[rr as usize][cc as usize] == 0 { open += 1; }
        }
        if run >= min_run && open >= 1 { count += 1; }
    }
    count
}

/// One root-level pass used by iterative deepening with aspiration windows.
fn id_root_pass(
    board: &Board,
    depth: u32,
    alpha: i64,
    beta: i64,
    player: i8,
    top: &[(i64, (usize, usize))],
    base_hash: u64,
    zt: &[[u64; 2]; N * N],
    shared_gid: &Arc<AtomicU64>,
    my_gid: u64,
    tt: &mut Vec<TtEntry>,
    eval_cache: &mut HashMap<u64, i64>,
    killers: &mut [[Option<(u8, u8)>; 2]; 8],
    history: &mut [[i32; N]; N],
) -> (i64, Option<(usize, usize)>) {
    let mut local_best = alpha;
    let mut local_move: Option<(usize, usize)> = None;
    let mut board_copy = *board;
    for &(_, (r, c)) in top {
        if shared_gid.load(Ordering::Relaxed) != my_gid {
            return (local_best, local_move);
        }
        board_copy[r][c] = player;
        let child_hash = base_hash ^ zt[r * N + c][(player - 1) as usize];
        let sc = minimax(
            &mut board_copy, depth - 1, local_best, beta,
            false, player, shared_gid, my_gid, child_hash, tt, eval_cache, killers, 0, history,
        );
        board_copy[r][c] = 0;
        if sc > local_best { local_best = sc; local_move = Some((r, c)); }
    }
    (local_best, local_move)
}

fn ai_best_move(
    board: &Board,
    player: i8,
    depth: u32,
    shared_gid: &Arc<AtomicU64>,
    my_gid: u64,
    seed: u64,
) -> Option<(usize, usize)> {
    let disable_opt = BENCH_DISABLE_OPT.load(Ordering::Relaxed);
    let mut rng = if seed == 0 { rng_seed() } else { seed };
    let opp = 3 - player;
    let start = Instant::now();
    let budget_ms: u128 = if disable_opt {
        u128::MAX
    } else if depth >= 5 {
        1300
    } else if depth >= 4 {
        900
    } else {
        650
    };

    // First move: pick random centre cell so every game opens differently
    let pieces: usize = board.iter().flatten().filter(|&&v| v != 0).count();
    if pieces == 0 {
        const OPTS: &[(usize, usize)] = &[
            (7, 7), (7, 8), (8, 7), (8, 8),
            (6, 7), (7, 6), (9, 7), (7, 9),
            (6, 8), (8, 6), (6, 6), (9, 9),
        ];
        return Some(OPTS[(xor64(&mut rng) as usize) % OPTS.len()]);
    }

    // Early opening shortcut: avoid expensive minimax tree when the board is nearly empty.
    // This removes the visible stall on the first minimax response move.
    if pieces <= 2 {
        return ai_greedy_move(board, player, seed);
    }

    let cands = candidates(board, 2);

    // Win immediately
    for &(r, c) in &cands {
        let mut b = *board;
        b[r][c] = player;
        if check_win(&b, r, c, player) {
            return Some((r, c));
        }
    }
    // Block opponent win
    for &(r, c) in &cands {
        let mut b = *board;
        b[r][c] = opp;
        if check_win(&b, r, c, opp) {
            return Some((r, c));
        }
    }

    if player == 1 {
        // Offensive (Black): try to build 4-in-a-row before worrying about lesser threats.
        for &(r, c) in &cands {
            if max_run_if_placed(board, r, c, player) >= 4 {
                return Some((r, c));
            }
        }
    } else {
        // Defensive (White): block opponent 4-in-a-row (forced win next move).
        for &(r, c) in &cands {
            if max_run_if_placed(board, r, c, opp) >= 4 {
                return Some((r, c));
            }
        }
    }

    // Fork: create two simultaneous open threats (unblockable)
    for &(r, c) in &cands {
        if open_fork_count(board, r, c, player, 3) >= 2 {
            return Some((r, c));
        }
    }
    // Block opponent open fork
    for &(r, c) in &cands {
        if open_fork_count(board, r, c, opp, 3) >= 2 {
            return Some((r, c));
        }
    }

    // Zobrist hash of current board + shared transposition/eval caches
    let zt = ZOBRIST.get_or_init(init_zobrist);
    let mut base_hash = 0u64;
    for r in 0..N {
        for c in 0..N {
            let v = board[r][c];
            if v != 0 { base_hash ^= zt[r * N + c][(v - 1) as usize]; }
        }
    }
    let tt_cell = SEARCH_TT.get_or_init(|| Mutex::new(vec![TtEntry { hash: 0, score: 0, best_move: (255, 255), depth: 0, flag: 0 }; TT_SIZE]));
    let eval_cell = SEARCH_EVAL_CACHE.get_or_init(|| Mutex::new(HashMap::new()));
    let mut tt_guard = tt_cell.lock().unwrap();
    let mut eval_guard = eval_cell.lock().unwrap();
    if disable_opt {
        for e in tt_guard.iter_mut() {
            *e = TtEntry { hash: 0, score: 0, best_move: (255, 255), depth: 0, flag: 0 };
        }
        eval_guard.clear();
    } else if eval_guard.len() > SEARCH_EVAL_CACHE_MAX {
        eval_guard.clear();
    }
    let mut history = [[0i32; N]; N];

    // Initial candidate ordering: quick_score + jitter
    let mut scored: Vec<(i64, (usize, usize))> = cands
        .iter()
        .map(|&(r, c)| {
            let mut b = *board;
            b[r][c] = player;
            let center_bias = if disable_opt {
                0
            } else {
                20 - ((r as i32 - 7).abs() + (c as i32 - 7).abs()) as i64
            };
            let base = quick_score(&b, r, c, player) + center_bias;
            let jitter = (xor64(&mut rng) % 9) as i64 - 4; // ±4
            (base + jitter, (r, c))
        })
        .collect();
    scored.sort_unstable_by(|a, b| b.0.cmp(&a.0));
    let root_take = if pieces < 8 { 8 } else { 12 };
    let mut top: Vec<_> = scored.into_iter().take(root_take).collect();

    // Iterative deepening: TT + history carry across depths; aspiration windows for d≥3
    let mut best_move: Option<(usize, usize)> = top.first().map(|&(_, m)| m);
    let mut prev_score = 0i64;

    for d in 1..=depth {
        if start.elapsed().as_millis() > budget_ms {
            break;
        }
        // PV ordering: previous best move to front
        if let Some(bm) = best_move {
            if let Some(idx) = top.iter().position(|&(_, m)| m == bm) {
                let item = top.remove(idx);
                top.insert(0, item);
            }
        }

        // Aspiration window: narrow bounds for d >= 3; full window on failure
        const ASP: i64 = 300_000;
        let use_asp = d >= 3 && prev_score.abs() < BIG / 2;
        let lo = if use_asp { prev_score - ASP } else { -BIG };
        let hi = if use_asp { prev_score + ASP } else {  BIG };

        let mut killers: [[Option<(u8, u8)>; 2]; 8] = [[None; 2]; 8];
        let (mut local_best, mut local_move) = id_root_pass(
            board, d, lo, hi, player, &top, base_hash, zt,
            shared_gid, my_gid, &mut tt_guard, &mut eval_guard, &mut killers, &mut history,
        );

        // Aspiration failure: re-search with full window
        if use_asp && (local_best <= lo || local_best >= hi) {
            killers = [[None; 2]; 8];
            let (lb, lm) = id_root_pass(
                board, d, -BIG, BIG, player, &top, base_hash, zt,
                shared_gid, my_gid, &mut tt_guard, &mut eval_guard, &mut killers, &mut history,
            );
            local_best = lb;
            local_move = lm;
        }

        if shared_gid.load(Ordering::Relaxed) != my_gid { return best_move; }
        prev_score = local_best;
        if let Some(m) = local_move { best_move = Some(m); }

        // Decay history: recent cutoffs stay relevant, old ones fade
        for row in history.iter_mut() {
            for v in row.iter_mut() { *v >>= 2; }
        }
    }

    best_move
}

fn ai_greedy_move(board: &Board, player: i8, seed: u64) -> Option<(usize, usize)> {
    let mut rng = if seed == 0 { rng_seed() } else { seed };
    let opp = 3 - player;
    let cands = candidates(board, 2);

    if cands.is_empty() { return None; }

    for &(r, c) in &cands {
        let mut b = *board;
        b[r][c] = player;
        if check_win(&b, r, c, player) { return Some((r, c)); }
    }
    for &(r, c) in &cands {
        let mut b = *board;
        b[r][c] = opp;
        if check_win(&b, r, c, opp) { return Some((r, c)); }
    }

    let mut best = i64::MIN;
    let mut best_moves: Vec<(usize, usize)> = Vec::new();
    for &(r, c) in &cands {
        let mut b = *board;
        b[r][c] = player;
        let mut sc = quick_score(&b, r, c, player);
        if max_run_if_placed(board, r, c, player) >= 4 { sc += 2_000_000; }
        if open_fork_count(board, r, c, player, 3) >= 2 { sc += 1_000_000; }
        if sc > best {
            best = sc;
            best_moves.clear();
            best_moves.push((r, c));
        } else if sc == best {
            best_moves.push((r, c));
        }
    }

    if best_moves.is_empty() {
        Some(cands[(xor64(&mut rng) as usize) % cands.len()])
    } else {
        Some(best_moves[(xor64(&mut rng) as usize) % best_moves.len()])
    }
}

fn ai_book_move(board: &Board, player: i8, seed: u64) -> Option<(usize, usize)> {
    let mut rng = if seed == 0 { rng_seed() } else { seed };
    let pieces: usize = board.iter().flatten().filter(|&&v| v != 0).count();
    let center = (N / 2, N / 2);
    let ring: &[(usize, usize)] = &[
        (7, 7), (7, 8), (8, 7), (8, 8),
        (6, 7), (7, 6), (8, 9), (9, 8),
        (6, 6), (6, 8), (8, 6), (9, 9),
    ];

    if pieces == 0 {
        return Some(ring[(xor64(&mut rng) as usize) % ring.len()]);
    }

    if pieces <= 6 {
        let mut opts: Vec<(usize, usize)> = ring.iter().copied().filter(|&(r, c)| board[r][c] == 0).collect();
        if !opts.is_empty() {
            return Some(opts.swap_remove((xor64(&mut rng) as usize) % opts.len()));
        }
        if board[center.0][center.1] == 0 { return Some(center); }
    }

    ai_greedy_move(board, player, seed)
}

fn ai_policy_move(board: &Board, player: i8, seed: u64) -> Option<(usize, usize)> {
    let mut rng = if seed == 0 { rng_seed() } else { seed };
    let cands = candidates(board, 2);
    if cands.is_empty() { return None; }

    let mut scored: Vec<(i64, (usize, usize))> = Vec::with_capacity(cands.len());
    for (r, c) in cands {
        let mut b = *board;
        b[r][c] = player;
        let mut s = quick_score(&b, r, c, player);
        if check_win(&b, r, c, player) { s += BIG / 4; }
        scored.push((s, (r, c)));
    }
    scored.sort_unstable_by(|a, b| b.0.cmp(&a.0));
    let top_k = scored.len().min(5);
    let pick = (xor64(&mut rng) as usize) % top_k;
    Some(scored[pick].1)
}

// ── TD learning ──────────────────────────────────────────────────────────────

/// Update learned value table after a game ends.
/// `winner` = 1/2 or 0 for draw. Values stored from `mcts_player` perspective.
fn td_update(positions: &[u64], winner: i8, mcts_player: i8,
             learned: &mut HashMap<u64, (f32, u32)>) {
    let n = positions.len();
    if n == 0 { return; }
    let outcome = if winner == mcts_player { 1.0f32 }
                  else if winner == 0      { 0.0 }
                  else                    { -1.0 };
    for (i, &hash) in positions.iter().enumerate() {
        // Positions near the end of the game weighted more (smaller decay)
        let decay = 0.95f32.powi((n - 1 - i) as i32);
        let entry = learned.entry(hash).or_insert((0.0f32, 0u32));
        entry.0 += outcome * decay;
        entry.1 += 1;
    }
}

// ── MCTS ─────────────────────────────────────────────────────────────────────

fn mcts_best_move(
    board: &Board,
    player: i8,
    n_sims: u32,
    learned: &HashMap<u64, (f32, u32)>,
    zt: &[[u64; 2]; N * N],
    base_hash: u64,
    shared_gid: &Arc<AtomicU64>,
    my_gid: u64,
    seed: u64,
) -> (Option<(usize, usize)>, u32) {
    let disable_opt = BENCH_DISABLE_OPT.load(Ordering::Relaxed);
    let mut rng = if seed == 0 { rng_seed() } else { seed };
    let opp = 3 - player;
    let cands = candidates(board, 2);

    // Quick forced heuristics (same priority as minimax)
    for &(r, c) in &cands {
        let mut b = *board; b[r][c] = player;
        if check_win(&b, r, c, player) { return (Some((r,c)), 1); }
    }
    for &(r, c) in &cands {
        let mut b = *board; b[r][c] = opp;
        if check_win(&b, r, c, opp) { return (Some((r,c)), 1); }
    }
    for &(r, c) in &cands {
        if max_run_if_placed(board, r, c, player) >= 4 { return (Some((r,c)), 1); }
    }
    for &(r, c) in &cands {
        if max_run_if_placed(board, r, c, opp) >= 4 { return (Some((r,c)), 1); }
    }
    for &(r, c) in &cands {
        if open_fork_count(board, r, c, player, 3) >= 2 { return (Some((r,c)), 1); }
    }
    for &(r, c) in &cands {
        if open_fork_count(board, r, c, opp, 3) >= 2 { return (Some((r,c)), 1); }
    }

    // Root untried: pre-sorted by quick_score so promising moves expand first
    let mut root_untried: Vec<(u8,u8)> = cands.iter()
        .map(|&(r,c)|(r as u8,c as u8)).collect();
    root_untried.sort_unstable_by_key(|&(r,c)| {
        let mut b = *board; b[r as usize][c as usize] = player;
        -(quick_score(&b, r as usize, c as usize, player) / 1000)
    });
    // Reuse previous best root move for same (hash,player) as first candidate.
    if !disable_opt {
        if let Some(h) = MCTS_HINTS.get_or_init(|| Mutex::new(HashMap::new())).lock().ok()
            .and_then(|m| m.get(&(base_hash, player)).copied())
        {
            if let Some(idx) = root_untried.iter().position(|&mv| mv == h) {
                let item = root_untried.remove(idx);
                root_untried.insert(0, item);
            }
        }
    }

    let mut arena: Vec<MctsNode> = Vec::with_capacity((n_sims as usize).min(8192) + 4);
    arena.push(MctsNode {
        visits: 0, value: 0.0, mv: (255,255),
        parent: u32::MAX, children: Vec::new(),
        untried: root_untried, whose_turn: player,
    });

    const C_UCT: f32 = 1.414;

    for sim in 0..n_sims {
        if sim % 128 == 0 && shared_gid.load(Ordering::Relaxed) != my_gid { break; }

        // ── 1. SELECT ────────────────────────────────────────────────────────
        let mut idx = 0u32;
        let mut cur_board = *board;
        let mut cur_hash  = base_hash;
        loop {
            if !arena[idx as usize].untried.is_empty() { break; }
            if arena[idx as usize].children.is_empty()  { break; } // terminal
            let pv_ln    = (arena[idx as usize].visits as f32).ln().max(0.0);
            let whose    = arena[idx as usize].whose_turn;
            let maximize = whose == player;
            // Clone children to avoid borrow conflict with arena indexing in closure
            let children: Vec<u32> = arena[idx as usize].children.clone();
            let best = children.into_iter().max_by(|&a, &b| {
                let na = &arena[a as usize];
                let nb = &arena[b as usize];
                let qa = if maximize { na.value } else { -na.value };
                let qb = if maximize { nb.value } else { -nb.value };
                let sa = if na.visits == 0 { f32::INFINITY }
                         else { qa / na.visits as f32 + C_UCT * (pv_ln / na.visits as f32).sqrt() };
                let sb = if nb.visits == 0 { f32::INFINITY }
                         else { qb / nb.visits as f32 + C_UCT * (pv_ln / nb.visits as f32).sqrt() };
                sa.partial_cmp(&sb).unwrap_or(std::cmp::Ordering::Equal)
            }).unwrap();
            let mv = arena[best as usize].mv;
            cur_board[mv.0 as usize][mv.1 as usize] = whose;
            cur_hash ^= zt[mv.0 as usize * N + mv.1 as usize][(whose - 1) as usize];
            idx = best;
        }

        // ── 2. EXPAND / EVALUATE ─────────────────────────────────────────────
        let outcome: f32;
        if arena[idx as usize].untried.is_empty() {
            // Terminal: last move into this node was a win (or board full = draw)
            let mv = arena[idx as usize].mv;
            outcome = if mv.0 < N as u8 {
                let prev = 3 - arena[idx as usize].whose_turn;
                if prev == player { 1.0 } else { -1.0 }
            } else { 0.0 };
        } else {
            let (mv, whose) = {
                let node = &mut arena[idx as usize];
                let pick = (xor64(&mut rng) as usize) % node.untried.len();
                let mv = node.untried.swap_remove(pick);
                (mv, node.whose_turn)
            };
            cur_board[mv.0 as usize][mv.1 as usize] = whose;
            cur_hash ^= zt[mv.0 as usize * N + mv.1 as usize][(whose - 1) as usize];
            let is_win  = check_win(&cur_board, mv.0 as usize, mv.1 as usize, whose);
            let pieces  = cur_board.iter().flatten().filter(|&&v| v != 0).count();
            let is_draw = !is_win && pieces >= N * N;
            let child_untried = if is_win || is_draw { Vec::new() } else {
                candidates(&cur_board, 2).into_iter().map(|(r,c)|(r as u8,c as u8)).collect()
            };
            let child_idx = arena.len() as u32;
            let child = MctsNode {
                visits: 0, value: 0.0, mv,
                parent: idx, children: Vec::new(),
                untried: child_untried, whose_turn: 3 - whose,
            };
            arena[idx as usize].children.push(child_idx);
            arena.push(child);
            idx = child_idx;
            outcome = if is_win {
                if whose == player { 1.0 } else { -1.0 }
            } else if is_draw {
                0.0
            } else {
                // Heuristic leaf: blend eval_board with learned table
                let raw = (eval_board(&cur_board, player) as f32 / 100_000_000.0).clamp(-1.0, 1.0);
                if let Some(&(total, cnt)) = learned.get(&cur_hash) {
                    let lv = if cnt > 0 { (total / cnt as f32).clamp(-1.0, 1.0) } else { 0.0 };
                    0.6 * raw + 0.4 * lv
                } else { raw }
            };
        }

        // ── 3. BACKPROP ───────────────────────────────────────────────────────
        let mut cur = idx;
        loop {
            arena[cur as usize].visits += 1;
            arena[cur as usize].value  += outcome;
            let p = arena[cur as usize].parent;
            if p == u32::MAX { break; }
            cur = p;
        }
    }

    // Best root child: robust child (most visits)
    let best = arena[0].children.iter().copied()
        .max_by_key(|&c| arena[c as usize].visits)
        .map(|c| { let mv = arena[c as usize].mv; (mv.0 as usize, mv.1 as usize) });
    if !disable_opt {
        if let Some((r, c)) = best {
            if let Ok(mut hints) = MCTS_HINTS.get_or_init(|| Mutex::new(HashMap::new())).lock() {
                if hints.len() > MCTS_HINTS_MAX {
                    hints.clear();
                }
                hints.insert((base_hash, player), (r as u8, c as u8));
            }
        }
    }
    (best, arena[0].visits)
}

fn mcts_best_move_parallel(
    board: &Board,
    player: i8,
    n_sims: u32,
    learned: Arc<HashMap<u64, (f32, u32)>>,
    zt: &[[u64; 2]; N * N],
    base_hash: u64,
    shared_gid: &Arc<AtomicU64>,
    my_gid: u64,
    seed: u64,
    workers: usize,
) -> (Option<(usize, usize)>, u32) {
    let max_workers = workers.max(1).min(n_sims.max(1) as usize);
    let min_chunk = 300u32;
    let by_chunk = (n_sims / min_chunk).max(1) as usize;
    let workers = max_workers.min(by_chunk).max(1);
    if workers <= 1 {
        return mcts_best_move(board, player, n_sims, &learned, zt, base_hash, shared_gid, my_gid, seed);
    }

    let base = n_sims / workers as u32;
    let rem = n_sims % workers as u32;
    let mut handles = Vec::with_capacity(workers);
    for i in 0..workers {
        let sims_i = base + if (i as u32) < rem { 1 } else { 0 };
        if sims_i == 0 { continue; }
        let board_i = *board;
        let zt_i = *zt;
        let shared_gid_i = Arc::clone(shared_gid);
        let learned_i = Arc::clone(&learned);
        let seed_i = seed ^ (i as u64).wrapping_mul(0x9E37_79B9_7F4A_7C15);
        handles.push(thread::spawn(move || {
            mcts_best_move(
                &board_i,
                player,
                sims_i,
                &learned_i,
                &zt_i,
                base_hash,
                &shared_gid_i,
                my_gid,
                seed_i,
            )
        }));
    }

    let mut votes: HashMap<(usize, usize), u32> = HashMap::new();
    let mut total_sims = 0u32;
    for h in handles {
        if let Ok((mv, sims_done)) = h.join() {
            total_sims = total_sims.saturating_add(sims_done);
            if let Some(m) = mv {
                votes.entry(m)
                    .and_modify(|v| *v = v.saturating_add(sims_done))
                    .or_insert(sims_done);
            }
        }
    }

    let best = votes.into_iter().max_by_key(|(_, s)| *s).map(|(m, _)| m);
    (best, total_sims)
}

// ── GUI ──────────────────────────────────────────────────────────────────────

impl eframe::App for App {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // Poll AI result — drop guard before calling place (borrow rules)
        let ai_done: Option<(usize, usize)> = match self.ai_result.try_lock() {
            Ok(mut g) => g.take(),
            Err(_) => None,
        };
        if let Some((row, col)) = ai_done {
            self.ai_busy.store(false, Ordering::SeqCst);
            if !self.over {
                self.place(row, col);
            }
        }

        // Launch AI if it's AI's turn (and not paused)
        if !self.over && !self.ai_busy.load(Ordering::Relaxed) && !self.paused {
            let needs_ai = match self.mode {
                Mode::HumanAI => self.turn == 2,
                Mode::AiAi | Mode::Auto => true,
            };
            if needs_ai {
                self.launch_ai(ctx.clone());
            }
        }

        // Auto-mode: restart after configurable pause when game ends
        if self.mode == Mode::Auto && self.over && !self.paused {
            if self.auto_start.is_none() { self.auto_start = Some(Instant::now()); }
            if self.auto_delay.is_none() {
                self.auto_delay = Some(Instant::now());
            }
            // Fixed 120ms inter-game pause — just enough to render the result
            if self.auto_delay.unwrap().elapsed() >= Duration::from_millis(120) {
                self.reset();
            }
            ctx.request_repaint_after(Duration::from_millis(30));
        }

        if self.ai_busy.load(Ordering::Relaxed) {
            let repaint_ms = if self.mode == Mode::Auto { 90 } else { 65 };
            ctx.request_repaint_after(Duration::from_millis(repaint_ms));
        }

        egui::CentralPanel::default().show(ctx, |ui| {
            const CELL: f32 = 38.0;
            const PAD: f32 = 34.0;   // extra room for coordinates
            let board_px = PAD * 2.0 + CELL * (N as f32 - 1.0);
            ui.set_max_width(board_px);

            // ── toolbar row 1: modes + actions ──
            ui.horizontal_wrapped(|ui| {
                ui.heading("Gomoku");
                ui.separator();
                if ui.selectable_label(self.mode == Mode::HumanAI, "Humano vs IA").clicked()
                    && self.mode != Mode::HumanAI
                { self.mode = Mode::HumanAI; self.reset(); }
                if ui.selectable_label(self.mode == Mode::AiAi, "IA vs IA").clicked()
                    && self.mode != Mode::AiAi
                { self.mode = Mode::AiAi; self.reset(); }
                if ui.selectable_label(self.mode == Mode::Auto, "Auto").clicked()
                    && self.mode != Mode::Auto
                { self.mode = Mode::Auto; self.reset(); }
                ui.separator();
                if ui.button("⟳ Nueva").clicked() { self.reset(); }
                if self.mode == Mode::HumanAI
                    && !self.history.is_empty()
                    && !self.ai_busy.load(Ordering::Relaxed)
                {
                    if ui.button("↩ Deshacer").clicked() { self.undo(); }
                }
                if self.mode == Mode::Auto {
                    let pause_lbl = if self.paused { "▶ Reanudar" } else { "⏸ Pausa" };
                    if ui.button(pause_lbl).clicked() { self.paused = !self.paused; }
                }
                if ui.button("Reset stats").clicked() { self.reset_stats(); }
            });

            // ── toolbar row 2: depth sliders + AI kind selector ──
            ui.horizontal_wrapped(|ui| {
                ui.colored_label(Color32::from_rgb(180,140,60), "⬤ Prof:");
                let mut db = self.ai_depth_black as f32;
                if ui.add(egui::Slider::new(&mut db, 1.0..=5.0).step_by(1.0).show_value(true)).changed() {
                    self.ai_depth_black = db as u32;
                    self.persist_prefs();
                }
                let mut pb = self.ai_profile_black;
                egui::ComboBox::from_id_source("black_ai_profile")
                    .selected_text(pb.label())
                    .show_ui(ui, |ui| {
                        ui.selectable_value(&mut pb, AiProfile::Minimax, AiProfile::Minimax.label());
                        ui.selectable_value(&mut pb, AiProfile::Mcts, AiProfile::Mcts.label());
                        ui.selectable_value(&mut pb, AiProfile::Greedy, AiProfile::Greedy.label());
                        ui.selectable_value(&mut pb, AiProfile::Pvs, AiProfile::Pvs.label());
                        ui.selectable_value(&mut pb, AiProfile::MctsRave, AiProfile::MctsRave.label());
                        ui.selectable_value(&mut pb, AiProfile::Book, AiProfile::Book.label());
                        ui.selectable_value(&mut pb, AiProfile::Policy, AiProfile::Policy.label());
                        ui.selectable_value(&mut pb, AiProfile::Hybrid, AiProfile::Hybrid.label());
                        ui.selectable_value(&mut pb, AiProfile::Random, AiProfile::Random.label());
                    });
                if pb != self.ai_profile_black {
                    self.ai_profile_black = pb;
                    self.persist_prefs();
                    if self.pieces == 0 && !self.over { self.pick_game_ai_kinds(); }
                }
                ui.small(format!("→ {}", self.game_kind_black.label()));
                ui.separator();
                ui.colored_label(Color32::from_rgb(200,200,200), "⬤ Prof:");
                let mut dw = self.ai_depth_white as f32;
                if ui.add(egui::Slider::new(&mut dw, 1.0..=5.0).step_by(1.0).show_value(true)).changed() {
                    self.ai_depth_white = dw as u32;
                    self.persist_prefs();
                }
                let mut pw = self.ai_profile_white;
                egui::ComboBox::from_id_source("white_ai_profile")
                    .selected_text(pw.label())
                    .show_ui(ui, |ui| {
                        ui.selectable_value(&mut pw, AiProfile::Minimax, AiProfile::Minimax.label());
                        ui.selectable_value(&mut pw, AiProfile::Mcts, AiProfile::Mcts.label());
                        ui.selectable_value(&mut pw, AiProfile::Greedy, AiProfile::Greedy.label());
                        ui.selectable_value(&mut pw, AiProfile::Pvs, AiProfile::Pvs.label());
                        ui.selectable_value(&mut pw, AiProfile::MctsRave, AiProfile::MctsRave.label());
                        ui.selectable_value(&mut pw, AiProfile::Book, AiProfile::Book.label());
                        ui.selectable_value(&mut pw, AiProfile::Policy, AiProfile::Policy.label());
                        ui.selectable_value(&mut pw, AiProfile::Hybrid, AiProfile::Hybrid.label());
                        ui.selectable_value(&mut pw, AiProfile::Random, AiProfile::Random.label());
                    });
                if pw != self.ai_profile_white {
                    self.ai_profile_white = pw;
                    self.persist_prefs();
                    if self.pieces == 0 && !self.over { self.pick_game_ai_kinds(); }
                }
                ui.small(format!("→ {}", self.game_kind_white.label()));
                ui.separator();
                let learned_size = self.learned.lock().map(|l| l.len()).unwrap_or(0);
                if learned_size > 0 {
                    ui.colored_label(Color32::from_rgb(120, 200, 255),
                        format!("🧠 {} pos", learned_size));
                }
                ui.separator();
                ui.label("Hilos MCTS:");
                let mut mt = self.mcts_threads_pref;
                egui::ComboBox::from_id_source("mcts_threads_pref")
                    .selected_text(match mt {
                        0 => "Auto",
                        1 => "1",
                        2 => "2",
                        4 => "4",
                        8 => "8",
                        _ => "Auto",
                    })
                    .show_ui(ui, |ui| {
                        ui.selectable_value(&mut mt, 0, "Auto");
                        ui.selectable_value(&mut mt, 1, "1");
                        ui.selectable_value(&mut mt, 2, "2");
                        ui.selectable_value(&mut mt, 4, "4");
                        ui.selectable_value(&mut mt, 8, "8");
                    });
                if mt != self.mcts_threads_pref {
                    self.mcts_threads_pref = mt;
                    self.persist_prefs();
                }
            });

            // ── ranking ──
            ui.horizontal_wrapped(|ui| {
                ui.label("Ranking IA:");
                let mut rows: Vec<(AiKind, RankEntry)> = self.rankings.iter().map(|(k, v)| (*k, *v)).collect();
                rows.sort_by(|a, b| {
                    b.1.points().partial_cmp(&a.1.points()).unwrap_or(std::cmp::Ordering::Equal)
                        .then_with(|| b.1.wins.cmp(&a.1.wins))
                });
                for (i, (kind, r)) in rows.iter().enumerate() {
                    let g = r.games();
                    let wr = if g > 0 { 100.0 * r.wins as f32 / g as f32 } else { 0.0 };
                    ui.colored_label(
                        if i == 0 { Color32::from_rgb(255, 220, 90) } else { Color32::from_rgb(190, 210, 230) },
                        format!("{}. {} W{} L{} D{} ({:.0}%)", i + 1, kind.label(), r.wins, r.losses, r.draws, wr)
                    );
                    ui.separator();
                }
                if ui.button("Reset ranking").clicked() { self.reset_rankings(); }
            });

            // ── stats bar ──
            ui.horizontal_wrapped(|ui| {
                let s = &self.stats;
                let total_f = s.total.max(1) as f32;
                let pct_b = s.wins_black as f32 / total_f * 100.0;
                let pct_w = s.wins_white as f32 / total_f * 100.0;

                // Win% ratio bar (120px wide)
                let (bar_rect, _) = ui.allocate_exact_size(Vec2::new(120.0, 14.0), egui::Sense::hover());
                let bw = bar_rect.width() * (s.wins_black as f32 / total_f);
                let ww = bar_rect.width() * (s.wins_white as f32 / total_f);
                ui.painter().rect_filled(bar_rect, 2.0, Color32::from_rgb(60, 60, 60));
                ui.painter().rect_filled(
                    egui::Rect::from_min_size(bar_rect.min, Vec2::new(bw, bar_rect.height())),
                    2.0, Color32::from_rgb(180, 140, 60));
                ui.painter().rect_filled(
                    egui::Rect::from_min_size(bar_rect.min + Vec2::new(bw, 0.0), Vec2::new(ww, bar_rect.height())),
                    0.0, Color32::from_rgb(210, 210, 210));

                ui.separator();
                // Win counts + streaks
                let str_b = if self.streak_black >= 2 { format!(" 🔥{}", self.streak_black) } else { String::new() };
                let str_w = if self.streak_white >= 2 { format!(" 🔥{}", self.streak_white) } else { String::new() };
                ui.colored_label(Color32::from_rgb(180, 140, 60),
                    format!("⬤ N: {} ({:.0}%){}", s.wins_black, pct_b, str_b));
                ui.separator();
                ui.colored_label(Color32::from_rgb(200, 200, 200),
                    format!("⬤ B: {} ({:.0}%){}", s.wins_white, pct_w, str_w));
                ui.separator();
                ui.colored_label(Color32::YELLOW, format!("= {}", s.draws));
                if self.mode == Mode::Auto {
                    ui.separator();
                    ui.colored_label(Color32::LIGHT_BLUE, format!("Total: {}", s.total));
                    if let Some(t0) = self.auto_start {
                        let mins = t0.elapsed().as_secs_f64() / 60.0;
                        if mins > 0.1 {
                            let gpm = s.total as f64 / mins;
                            ui.colored_label(Color32::from_rgb(100, 220, 180),
                                format!("({:.1}/min)", gpm));
                        }
                    }
                }
                let sims = self.mcts_sims.load(Ordering::Relaxed);
                if sims > 0 {
                    ui.separator();
                    ui.colored_label(Color32::from_rgb(150, 200, 255),
                        format!("MCTS ▲{}sims", sims));
                }
            });

            // ── status ──
            ui.horizontal_wrapped(|ui| {
                if self.over {
                    if self.draw {
                        ui.colored_label(Color32::YELLOW, "Empate");
                    } else {
                        let name = if self.winner == Some(1) { "Negro" } else { "Blanco" };
                        ui.colored_label(Color32::from_rgb(80, 255, 80), format!("¡Gana {}!", name));
                    }
                } else {
                    let thinking = self.ai_busy.load(Ordering::Relaxed);
                    let name = if self.turn == 1 { "Negro" } else { "Blanco" };
                    let suffix = if thinking { " (pensando...)" } else { "" };
                    let ai_lbl = if self.turn == 1 { self.game_kind_black.label() } else { self.game_kind_white.label() };
                    let last_str = match self.last_mv {
                        Some((r, c)) => format!(" | Último: {}{}", (b'A' + c as u8) as char, r + 1),
                        None => String::new(),
                    };
                    ui.label(format!("Turno: {}{} [{}] | Jugadas: {}{}", name, suffix, ai_lbl, self.pieces, last_str));
                }
            });

            ui.separator();

            // ── board ──
            let (resp, painter) =
                ui.allocate_painter(Vec2::splat(board_px), egui::Sense::click());

            // Wood background
            painter.rect_filled(resp.rect, 4.0, Color32::from_rgb(205, 165, 90));

            let ox = resp.rect.min.x + PAD;
            let oy = resp.rect.min.y + PAD;
            let ex = ox + CELL * (N as f32 - 1.0);
            let ey = oy + CELL * (N as f32 - 1.0);
            let line_col = Color32::from_rgb(70, 45, 15);
            let coord_col = Color32::from_rgb(50, 30, 10);

            // Grid
            for i in 0..N {
                let x = ox + i as f32 * CELL;
                let y = oy + i as f32 * CELL;
                painter.line_segment([Pos2::new(ox, y), Pos2::new(ex, y)], Stroke::new(1.0, line_col));
                painter.line_segment([Pos2::new(x, oy), Pos2::new(x, ey)], Stroke::new(1.0, line_col));
                // Column labels (A-O) at top
                let col_lbl = (b'A' + i as u8) as char;
                painter.text(Pos2::new(x, oy - 18.0), egui::Align2::CENTER_CENTER,
                    col_lbl.to_string(), egui::FontId::monospace(11.0), coord_col);
                // Row labels (1-15) at left
                painter.text(Pos2::new(ox - 20.0, y), egui::Align2::CENTER_CENTER,
                    (i + 1).to_string(), egui::FontId::monospace(11.0), coord_col);
            }

            // Hoshi (star points)
            for &hr in &[3usize, 7, 11] {
                for &hc in &[3usize, 7, 11] {
                    let pos = Pos2::new(ox + hc as f32 * CELL, oy + hr as f32 * CELL);
                    painter.circle_filled(pos, 4.5, line_col);
                }
            }

            // Stones
            const R_STONE: f32 = CELL * 0.44;
            for r in 0..N {
                for c in 0..N {
                    let v = self.board[r][c];
                    if v == 0 { continue; }
                    let pos = Pos2::new(ox + c as f32 * CELL, oy + r as f32 * CELL);
                    let (fill, outline) = if v == 1 {
                        (Color32::from_rgb(25, 25, 25), Color32::from_rgb(100, 100, 100))
                    } else {
                        (Color32::from_rgb(245, 238, 215), Color32::from_rgb(130, 130, 130))
                    };
                    painter.circle_filled(pos, R_STONE, fill);
                    painter.circle_stroke(pos, R_STONE, Stroke::new(1.5, outline));
                    if self.winning_cells.contains(&(r, c)) {
                        painter.circle_stroke(pos, R_STONE - 3.0,
                            Stroke::new(3.0, Color32::from_rgb(255, 210, 30)));
                    }
                    if self.last_mv == Some((r, c)) {
                        let dot = if v == 1 { Color32::WHITE } else { Color32::BLACK };
                        painter.circle_filled(pos, 4.0, dot);
                    }
                }
            }

            // Human click
            if !self.over && !self.ai_busy.load(Ordering::Relaxed) {
                let human_turn = match self.mode {
                    Mode::HumanAI => self.turn == 1,
                    Mode::AiAi | Mode::Auto => false,
                };
                if human_turn && resp.clicked() {
                    if let Some(pos) = resp.interact_pointer_pos() {
                        let col = ((pos.x - ox + CELL / 2.0) / CELL).floor() as i32;
                        let row = ((pos.y - oy + CELL / 2.0) / CELL).floor() as i32;
                        if (0..N as i32).contains(&row) && (0..N as i32).contains(&col) {
                            let (r, c) = (row as usize, col as usize);
                            if self.board[r][c] == 0 {
                                self.save_snapshot();
                                self.place(r, c);
                            }
                        }
                    }
                }
            }
        });
    }

    fn on_exit(&mut self, _gl: Option<&eframe::glow::Context>) {
        self.persist_prefs();
        self.persist_rankings();
        self.persist_learning();
    }
}

#[derive(Clone, Copy)]
struct BenchRow {
    minimax_ms_avg: f64,
    mcts_ms_avg: f64,
    games_per_min: f64,
    plies_per_min: f64,
    plies_per_game: f64,
}

fn bench_pick_ai_move(board: &Board, player: i8, kind: AiKind, depth: u32, n_sims: u32, seed: u64) -> Option<(usize, usize)> {
    let gid = Arc::new(AtomicU64::new(1));
    let my_gid = 1u64;
    let zt = ZOBRIST.get_or_init(init_zobrist);
    let hash = zobrist_hash(board, zt);
    match kind {
        AiKind::Minimax => ai_best_move(board, player, depth, &gid, my_gid, seed),
        AiKind::Mcts => {
            let learned = Arc::new(HashMap::<u64, (f32, u32)>::new());
            let workers = mcts_worker_count(0);
            let (m, _) = if workers <= 1 || n_sims < 1200 {
                mcts_best_move(board, player, n_sims, &learned, zt, hash, &gid, my_gid, seed)
            } else {
                mcts_best_move_parallel(board, player, n_sims, learned, zt, hash, &gid, my_gid, seed, workers)
            };
            m
        }
        AiKind::Greedy => ai_greedy_move(board, player, seed),
        AiKind::Pvs => ai_best_move(board, player, (depth + 1).min(6), &gid, my_gid, seed),
        AiKind::MctsRave => {
            let learned = Arc::new(HashMap::<u64, (f32, u32)>::new());
            let workers = mcts_worker_count(0);
            let sims = n_sims.saturating_mul(3) / 2;
            let (m, _) = if workers <= 1 || sims < 1200 {
                mcts_best_move(board, player, sims, &learned, zt, hash, &gid, my_gid, seed)
            } else {
                mcts_best_move_parallel(board, player, sims, learned, zt, hash, &gid, my_gid, seed, workers)
            };
            m
        }
        AiKind::Book => ai_book_move(board, player, seed),
        AiKind::Policy => ai_policy_move(board, player, seed),
        AiKind::Hybrid => {
            let pieces = board.iter().flatten().filter(|&&v| v != 0).count();
            if pieces < 8 {
                ai_book_move(board, player, seed)
            } else {
                let learned = Arc::new(HashMap::<u64, (f32, u32)>::new());
                let workers = mcts_worker_count(0);
                let (m, _) = if workers <= 1 || n_sims < 1200 {
                    mcts_best_move(board, player, n_sims, &learned, zt, hash, &gid, my_gid, seed)
                } else {
                    mcts_best_move_parallel(board, player, n_sims, learned, zt, hash, &gid, my_gid, seed, workers)
                };
                m.or_else(|| ai_best_move(board, player, depth, &gid, my_gid, seed))
            }
        }
    }
}

fn run_bench_once(disable_opt: bool) -> BenchRow {
    BENCH_DISABLE_OPT.store(disable_opt, Ordering::Relaxed);

    let depth = 3u32;
    let n_sims = 3000u32;
    let samples = 8u32;

    let mut mm_total = 0.0f64;
    let mut mcts_total = 0.0f64;

    for i in 0..samples {
        let mut board = [[0i8; N]; N];
        let center = (N / 2, N / 2);
        board[center.0][center.1] = 1;
        let mut seed = (i as u64 + 1).wrapping_mul(0x9E37_79B9_7F4A_7C15);
        for _ in 0..8 {
            let cands = candidates(&board, 2);
            if cands.is_empty() { break; }
            let pick = (xor64(&mut seed) as usize) % cands.len();
            let (r, c) = cands[pick];
            let p = if xor64(&mut seed) & 1 == 0 { 1 } else { 2 };
            board[r][c] = p;
        }

        let t0 = Instant::now();
        let _ = bench_pick_ai_move(&board, 1, AiKind::Minimax, depth, n_sims, seed ^ 0xA5A5);
        mm_total += t0.elapsed().as_secs_f64() * 1000.0;

        let t1 = Instant::now();
        let _ = bench_pick_ai_move(&board, 2, AiKind::Mcts, depth, n_sims, seed ^ 0x5A5A);
        mcts_total += t1.elapsed().as_secs_f64() * 1000.0;
    }

    let games = 4u32;
    let t_games = Instant::now();
    let mut total_plies = 0u32;
    for g in 0..games {
        let mut board = [[0i8; N]; N];
        let mut turn = 1i8;
        let mut over = false;
        let mut seed = 0xDEAD_BEEFu64 ^ g as u64;
        for _ply in 0..(N * N) {
            let kind = if turn == 1 { AiKind::Hybrid } else { AiKind::MctsRave };
            let mv = bench_pick_ai_move(&board, turn, kind, 3, 1500, seed);
            let Some((r, c)) = mv else { break; };
            if board[r][c] != 0 { break; }
            board[r][c] = turn;
            total_plies = total_plies.saturating_add(1);
            if check_win(&board, r, c, turn) {
                over = true;
                break;
            }
            if board.iter().flatten().all(|&v| v != 0) {
                over = true;
                break;
            }
            turn = 3 - turn;
            seed = xor64(&mut seed);
        }
        if !over {
            let _ = board;
        }
    }
    let mins = t_games.elapsed().as_secs_f64() / 60.0;
    let gpm = if mins > 0.0 { games as f64 / mins } else { 0.0 };
    let ppm = if mins > 0.0 { total_plies as f64 / mins } else { 0.0 };
    let ppg = if games > 0 { total_plies as f64 / games as f64 } else { 0.0 };

    BenchRow {
        minimax_ms_avg: mm_total / samples as f64,
        mcts_ms_avg: mcts_total / samples as f64,
        games_per_min: gpm,
        plies_per_min: ppm,
        plies_per_game: ppg,
    }
}

fn run_cli_benchmark() {
    let before = run_bench_once(true);
    let after = run_bench_once(false);
    let mut out = String::new();
    out.push_str("metric\tbefore\tafter\timprovement\n");
    let mm_imp = if before.minimax_ms_avg > 0.0 {
        (before.minimax_ms_avg - after.minimax_ms_avg) * 100.0 / before.minimax_ms_avg
    } else { 0.0 };
    let mcts_imp = if before.mcts_ms_avg > 0.0 {
        (before.mcts_ms_avg - after.mcts_ms_avg) * 100.0 / before.mcts_ms_avg
    } else { 0.0 };
    let gpm_imp = if before.games_per_min > 0.0 {
        (after.games_per_min - before.games_per_min) * 100.0 / before.games_per_min
    } else { 0.0 };
    let ppm_imp = if before.plies_per_min > 0.0 {
        (after.plies_per_min - before.plies_per_min) * 100.0 / before.plies_per_min
    } else { 0.0 };
    let ppg_imp = if before.plies_per_game > 0.0 {
        (after.plies_per_game - before.plies_per_game) * 100.0 / before.plies_per_game
    } else { 0.0 };
    out.push_str(&format!("minimax_ms_avg\t{:.2}\t{:.2}\t{:+.1}%\n", before.minimax_ms_avg, after.minimax_ms_avg, mm_imp));
    out.push_str(&format!("mcts_ms_avg\t{:.2}\t{:.2}\t{:+.1}%\n", before.mcts_ms_avg, after.mcts_ms_avg, mcts_imp));
    out.push_str(&format!("games_per_min\t{:.2}\t{:.2}\t{:+.1}%\n", before.games_per_min, after.games_per_min, gpm_imp));
    out.push_str(&format!("plies_per_min\t{:.2}\t{:.2}\t{:+.1}%\n", before.plies_per_min, after.plies_per_min, ppm_imp));
    out.push_str(&format!("plies_per_game\t{:.2}\t{:.2}\t{:+.1}%\n", before.plies_per_game, after.plies_per_game, ppg_imp));

    let p = std::env::current_exe()
        .ok()
        .and_then(|e| e.parent().map(|d| d.join("gomoku_bench.tsv")))
        .unwrap_or_else(|| PathBuf::from("gomoku_bench.tsv"));
    let _ = fs::write(&p, &out);
    let _ = fs::write("gomoku_bench.tsv", &out);
    if let Ok(cd) = std::env::current_dir() {
        let _ = fs::write(cd.join("gomoku_bench.tsv"), &out);
    }
    println!("{}", out);
}

fn main() -> eframe::Result<()> {
    let args: Vec<String> = std::env::args().collect();
    let _ = fs::write("gomoku_args.txt", args.join("\n"));
    if args.iter().any(|a| a == "--bench" || a == "bench") {
        run_cli_benchmark();
        return Ok(());
    }
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_title("Gomoku – 5 en Raya")
            .with_inner_size([700.0, 820.0])
            .with_resizable(true),
        ..Default::default()
    };
    eframe::run_native(
        "Gomoku",
        options,
        Box::new(|_cc| Ok(Box::new(App::new()))),
    )
}
