"""
Gomoku — 5 en Raya  |  15×15
Modos: Humano (Negro) vs IA (Blanco)  /  IA vs IA
"""
import tkinter as tk
from tkinter import messagebox
import threading
import time

# ── Constantes ────────────────────────────────────────────────
BOARD  = 15
CELL   = 40
PAD    = 32
WIN    = 5
EMPTY, BLACK, WHITE = 0, 1, 2
NAMES  = {BLACK: 'Negro ●', WHITE: 'Blanco ○'}
PCOLOR = {BLACK: '#111111', WHITE: '#f0e8d0'}

# ── Lógica IA ─────────────────────────────────────────────────

def check_win(board, row, col, player):
    for dr, dc in ((0,1),(1,0),(1,1),(1,-1)):
        cnt = 1
        for s in (1,-1):
            r, c = row+dr*s, col+dc*s
            while 0<=r<BOARD and 0<=c<BOARD and board[r][c]==player:
                cnt+=1; r+=dr*s; c+=dc*s
        if cnt >= WIN:
            return True
    return False


def _score_window(win, player):
    opp = 3-player
    if opp in win:
        return 0
    p = win.count(player)
    if p==5: return 10_000_000
    if p==4: return  100_000
    if p==3: return    1_000
    if p==2: return      100
    return 10


def _eval(board, player):
    opp = 3-player
    my = op = 0
    N = BOARD
    for r in range(N):
        for c in range(N-WIN+1):
            w=[board[r][c+i] for i in range(WIN)]
            my+=_score_window(w,player); op+=_score_window(w,opp)
    for c in range(N):
        for r in range(N-WIN+1):
            w=[board[r+i][c] for i in range(WIN)]
            my+=_score_window(w,player); op+=_score_window(w,opp)
    for r in range(N-WIN+1):
        for c in range(N-WIN+1):
            w=[board[r+i][c+i] for i in range(WIN)]
            my+=_score_window(w,player); op+=_score_window(w,opp)
    for r in range(N-WIN+1):
        for c in range(WIN-1,N):
            w=[board[r+i][c-i] for i in range(WIN)]
            my+=_score_window(w,player); op+=_score_window(w,opp)
    return my - int(op*1.1)


def _cands(board, radius=2):
    seen=set(); any_p=False
    for r in range(BOARD):
        for c in range(BOARD):
            if board[r][c]!=EMPTY:
                any_p=True
                for dr in range(-radius,radius+1):
                    for dc in range(-radius,radius+1):
                        nr,nc=r+dr,c+dc
                        if 0<=nr<BOARD and 0<=nc<BOARD and board[nr][nc]==EMPTY:
                            seen.add((nr,nc))
    return list(seen) if any_p else [(BOARD//2, BOARD//2)]


def _minimax(board, depth, alpha, beta, maxi, ai, gid_ref, gid):
    if gid_ref[0]!=gid:           # partida reiniciada
        return 0
    cur = ai if maxi else (3-ai)
    cands = _cands(board, radius=1)
    if not cands or depth==0:
        return _eval(board, ai)

    scored=[]
    for r,c in cands:
        board[r][c]=cur
        if check_win(board,r,c,cur):
            board[r][c]=EMPTY
            return 99_999_999 if maxi else -99_999_999
        scored.append((_eval(board,ai),r,c))
        board[r][c]=EMPTY
    scored.sort(reverse=maxi)

    if maxi:
        best=float('-inf')
        for _,r,c in scored[:8]:
            board[r][c]=cur
            v=_minimax(board,depth-1,alpha,beta,False,ai,gid_ref,gid)
            board[r][c]=EMPTY
            best=max(best,v); alpha=max(alpha,v)
            if beta<=alpha: break
        return best
    else:
        best=float('inf')
        for _,r,c in scored[:8]:
            board[r][c]=cur
            v=_minimax(board,depth-1,alpha,beta,True,ai,gid_ref,gid)
            board[r][c]=EMPTY
            best=min(best,v); beta=min(beta,v)
            if beta<=alpha: break
        return best


def ai_move(board, player, depth=3, gid_ref=None, gid=0):
    opp=3-player
    cands=_cands(board)

    for r,c in cands:           # ganar ya
        board[r][c]=player
        w=check_win(board,r,c,player)
        board[r][c]=EMPTY
        if w: return r,c

    for r,c in cands:           # bloquear rival
        board[r][c]=opp
        w=check_win(board,r,c,opp)
        board[r][c]=EMPTY
        if w: return r,c

    scored=[]
    for r,c in cands:
        board[r][c]=player
        scored.append((_eval(board,player),r,c))
        board[r][c]=EMPTY
    scored.sort(reverse=True)
    top=[(r,c) for _,r,c in scored[:12]]

    best,move=float('-inf'),top[0]
    for r,c in top:
        if gid_ref and gid_ref[0]!=gid: break
        board[r][c]=player
        s=_minimax(board,depth-1,float('-inf'),float('inf'),False,player,
                   gid_ref or [gid], gid)
        board[r][c]=EMPTY
        if s>best: best,move=s,(r,c)
    return move


# ── GUI ───────────────────────────────────────────────────────

class App:
    def __init__(self, root):
        self.root=root
        root.title('5 en Raya — Gomoku')
        root.resizable(False,False)
        root.configure(bg='#2b1a0a')

        self.mode=tk.StringVar(value='human_ai')
        self.board=[[EMPTY]*BOARD for _ in range(BOARD)]
        self.current=BLACK
        self.over=False
        self.thinking=False
        self.last=None
        self._gid=[0]           # game-id para cancelar hilos viejos

        self._ui()
        self._draw()

    # ── construcción UI ───────────────────────────────────────

    def _ui(self):
        bar=tk.Frame(self.root,bg='#4a2a0e',pady=7); bar.pack(fill=tk.X)
        tk.Label(bar,text='Modo:',bg='#4a2a0e',fg='#f0c060',
                 font=('Segoe UI',10,'bold')).pack(side=tk.LEFT,padx=12)
        for txt,val in (('Humano ⬛  vs  IA ⬜','human_ai'),
                        ('IA ⬛  vs  IA ⬜','ai_ai')):
            tk.Radiobutton(bar,text=txt,variable=self.mode,value=val,
                           bg='#4a2a0e',fg='#f0c060',selectcolor='#2b1a0a',
                           activebackground='#4a2a0e',activeforeground='#f0c060',
                           font=('Segoe UI',9),command=self.new_game
                           ).pack(side=tk.LEFT,padx=8)
        tk.Button(bar,text='⟳ Nueva partida',command=self.new_game,
                  bg='#a05010',fg='white',font=('Segoe UI',9,'bold'),
                  relief=tk.FLAT,padx=10,cursor='hand2'
                  ).pack(side=tk.RIGHT,padx=12)

        self.sv=tk.StringVar(value=f'Turno: {NAMES[BLACK]}')
        tk.Label(self.root,textvariable=self.sv,bg='#6b3a10',fg='#fff8e8',
                 font=('Segoe UI',11,'bold'),pady=5).pack(fill=tk.X)

        sz=CELL*(BOARD-1)+2*PAD
        self.cv=tk.Canvas(self.root,width=sz,height=sz,bg='#c8a040',
                          highlightthickness=0)
        self.cv.pack(padx=10,pady=10)
        self.cv.bind('<Button-1>',self._click)

    # ── dibujo ────────────────────────────────────────────────

    def _draw(self):
        self.cv.delete('all')
        sz=CELL*(BOARD-1)+2*PAD

        # Cuadrícula
        for i in range(BOARD):
            x=PAD+i*CELL; y=PAD+i*CELL
            self.cv.create_line(PAD,y,sz-PAD,y,fill='#5c3000',width=1)
            self.cv.create_line(x,PAD,x,sz-PAD,fill='#5c3000',width=1)
            self.cv.create_text(PAD-16,y,text=str(i+1),fill='#3b1800',
                                font=('Courier',8))
            self.cv.create_text(x,sz-PAD+14,text=chr(65+i),fill='#3b1800',
                                font=('Courier',8))

        # Puntos hoshi
        for r,c in [(3,3),(3,7),(3,11),(7,3),(7,7),(7,11),(11,3),(11,7),(11,11)]:
            x=PAD+c*CELL; y=PAD+r*CELL
            self.cv.create_oval(x-4,y-4,x+4,y+4,fill='#5c3000',outline='')

        # Piezas
        rad=CELL//2-3
        for r in range(BOARD):
            for c in range(BOARD):
                p=self.board[r][c]
                if p==EMPTY: continue
                x=PAD+c*CELL; y=PAD+r*CELL
                ol='#888888' if p==WHITE else '#000000'
                self.cv.create_oval(x-rad,y-rad,x+rad,y+rad,
                                    fill=PCOLOR[p],outline=ol,width=1.5)

        # Marca última jugada
        if self.last:
            r,c=self.last
            x=PAD+c*CELL; y=PAD+r*CELL
            col='#cc0000' if self.board[r][c]==WHITE else '#ff5555'
            self.cv.create_line(x-6,y,x+6,y,fill=col,width=2)
            self.cv.create_line(x,y-6,x,y+6,fill=col,width=2)

    # ── lógica de partida ─────────────────────────────────────

    def _click(self,evt):
        if self.over or self.thinking: return
        if self.mode.get()=='ai_ai': return
        if self.current==WHITE: return          # turno IA
        col=round((evt.x-PAD)/CELL)
        row=round((evt.y-PAD)/CELL)
        if 0<=row<BOARD and 0<=col<BOARD and self.board[row][col]==EMPTY:
            self._place(row,col)

    def _place(self,row,col):
        self.board[row][col]=self.current
        self.last=(row,col)
        self._draw()

        if check_win(self.board,row,col,self.current):
            name=NAMES[self.current]
            self.over=True
            self.sv.set(f'¡{name} gana!')
            self.root.after(200, lambda:
                messagebox.showinfo('Fin de partida',f'¡{name} gana!'))
            return

        if all(self.board[r][c]!=EMPTY for r in range(BOARD) for c in range(BOARD)):
            self.over=True
            self.sv.set('¡Empate! Tablero lleno.')
            self.root.after(200, lambda:
                messagebox.showinfo('Empate','¡Tablero lleno!'))
            return

        self.current=WHITE if self.current==BLACK else BLACK

        if self._is_ai():
            self.thinking=True
            self.sv.set(f'Turno: {NAMES[self.current]} — pensando…')
            gid=self._gid[0]
            threading.Thread(target=self._ai_thread,args=(gid,),daemon=True).start()
        else:
            self.sv.set(f'Turno: {NAMES[self.current]}')

    def _is_ai(self):
        if self.mode.get()=='ai_ai': return True
        return self.current==WHITE

    def _ai_thread(self,gid):
        time.sleep(0.3)
        if self._gid[0]!=gid: return
        move=ai_move(self.board,self.current,depth=3,
                     gid_ref=self._gid,gid=gid)
        self.thinking=False
        if move and not self.over and self._gid[0]==gid:
            self.root.after(0,lambda: self._place(*move)
                            if self._gid[0]==gid else None)

    def new_game(self):
        self._gid[0]+=1             # invalida hilos en vuelo
        self.over=False
        self.thinking=False
        self.board=[[EMPTY]*BOARD for _ in range(BOARD)]
        self.current=BLACK
        self.last=None
        self._draw()
        if self.mode.get()=='ai_ai':
            self.thinking=True
            self.sv.set(f'Turno: {NAMES[BLACK]} — pensando…')
            gid=self._gid[0]
            threading.Thread(target=self._ai_thread,args=(gid,),daemon=True).start()
        else:
            self.sv.set(f'Turno: {NAMES[BLACK]}')


# ── Main ──────────────────────────────────────────────────────
if __name__=='__main__':
    root=tk.Tk()
    App(root)
    root.mainloop()
