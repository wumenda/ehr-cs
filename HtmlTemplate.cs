namespace EhrOvertimeTray;

/// <summary>Web 面板页面（单文件自含，明暗自适应，年轻化视觉：柔和渐变 + 毛玻璃卡片 + 高饱和强调色）</summary>
public static class HtmlTemplate
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>加班时长 · EhrOvertimeTray</title>
<style>
  :root{
    --bg1:#fdf2f8; --bg2:#eff6ff; --bg3:#ecfeff;
    --card:rgba(255,255,255,.72); --card-strong:rgba(255,255,255,.88);
    --line:rgba(255,255,255,.85); --shadow:0 10px 34px rgba(139,92,246,.10), 0 2px 8px rgba(30,41,59,.04);
    --fg:#1e293b; --sub:#7c8aa0;
    --pink:#ec4899; --violet:#8b5cf6; --blue:#3b82f6; --cyan:#06b6d4;
    --grad:linear-gradient(135deg,#ec4899,#8b5cf6 52%,#3b82f6);
    --grad-soft:linear-gradient(135deg,rgba(236,72,153,.14),rgba(139,92,246,.14) 55%,rgba(59,130,246,.12));
    --green:#22c55e; --amber:#f59e0b; --red:#ef4444;
    --ok:#16a34a; --okbg:rgba(34,197,94,.14);
  }
  @media (prefers-color-scheme:dark){
    :root{
      --bg1:#241a33; --bg2:#16223c; --bg3:#0d2130;
      --card:rgba(30,32,52,.66); --card-strong:rgba(36,38,62,.92);
      --line:rgba(255,255,255,.10); --shadow:0 10px 34px rgba(0,0,0,.35);
      --fg:#eef2f8; --sub:#97a3b8;
      --grad:linear-gradient(135deg,#f472b6,#a78bfa 52%,#60a5fa);
      --grad-soft:linear-gradient(135deg,rgba(244,114,182,.16),rgba(167,139,250,.16) 55%,rgba(96,165,250,.14));
      --ok:#4ade80; --okbg:rgba(74,222,128,.16);
    }
  }
  *{box-sizing:border-box;margin:0;padding:0}
  body{
    font-family:"Segoe UI","PingFang SC","Microsoft YaHei",system-ui,-apple-system,sans-serif;
    color:var(--fg); min-height:100vh; padding:28px clamp(14px,4vw,40px) 48px;
    background:
      radial-gradient(620px 420px at 8% -6%, rgba(236,72,153,.20), transparent 62%),
      radial-gradient(720px 520px at 94% -4%, rgba(59,130,246,.18), transparent 62%),
      radial-gradient(640px 520px at 50% 112%, rgba(6,182,212,.15), transparent 62%),
      linear-gradient(160deg,var(--bg1),var(--bg2) 52%,var(--bg3));
    background-attachment:fixed;
  }
  .wrap{max-width:980px;margin:0 auto}
  header{display:flex;align-items:center;gap:14px;flex-wrap:wrap;margin-bottom:22px}
  .brand{display:flex;align-items:center;gap:10px;font-size:22px;font-weight:700;letter-spacing:.3px}
  .dot{width:14px;height:14px;border-radius:50%;background:var(--grad);box-shadow:0 0 0 5px rgba(139,92,246,.14)}
  .user{background:var(--card);border:1px solid var(--line);border-radius:999px;padding:6px 14px;font-size:12.5px;color:var(--sub);backdrop-filter:blur(12px);-webkit-backdrop-filter:blur(12px)}
  .op{margin-left:auto;display:flex;gap:10px;align-items:center;flex-wrap:wrap}
  .updated{color:var(--sub);font-size:12px}
  .btn{
    background:var(--grad);color:#fff;border:0;border-radius:999px;padding:9px 18px;
    font-size:13px;font-weight:600;cursor:pointer;box-shadow:0 6px 18px rgba(139,92,246,.32);
    transition:transform .15s ease, box-shadow .15s ease;
  }
  .btn:hover{transform:translateY(-1px);box-shadow:0 9px 24px rgba(139,92,246,.40)}
  .btn:active{transform:translateY(0)}
  .err{color:var(--red);font-size:13px;margin-bottom:14px;font-weight:500}

  .hero{
    background:var(--grad);color:#fff;border-radius:26px;padding:30px 30px 26px;margin-bottom:16px;
    box-shadow:0 18px 44px rgba(139,92,246,.30); position:relative;overflow:hidden;
  }
  .hero::after{content:"";position:absolute;right:-70px;top:-70px;width:220px;height:220px;border-radius:50%;
    background:rgba(255,255,255,.14)}
  .hero .label{font-size:13px;opacity:.92;letter-spacing:1px}
  .hero .big{font-size:52px;font-weight:800;line-height:1.15;font-variant-numeric:tabular-nums;margin:2px 0}
  .hero .range{display:inline-block;background:rgba(255,255,255,.20);border-radius:999px;padding:4px 12px;font-size:12px;margin-top:8px}
  .hero .sub{display:inline-block;background:rgba(255,255,255,.20);border-radius:999px;padding:4px 12px;font-size:12px;margin-top:8px;margin-left:8px}

  .cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:14px;margin-bottom:16px}
  .card{
    background:var(--card);border:1px solid var(--line);border-radius:20px;padding:18px 18px 16px;
    backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px); box-shadow:var(--shadow);
    animation:pop .4s ease backwards;
    transition:transform .15s ease;
  }
  .card:nth-child(2){animation-delay:.05s}.card:nth-child(3){animation-delay:.10s}.card:nth-child(4){animation-delay:.15s}
  .card:hover{transform:translateY(-2px)}
  @keyframes pop{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}
  .card .top{display:flex;align-items:center;gap:8px;margin-bottom:10px}
  .ico{width:30px;height:30px;border-radius:10px;display:flex;align-items:center;justify-content:center;font-size:15px}
  .card .label{color:var(--sub);font-size:12.5px}
  .card .value{font-size:26px;font-weight:800;font-variant-numeric:tabular-nums;line-height:1.2}
  .card .foot{color:var(--sub);font-size:11.5px;margin-top:4px}

  .panel{
    background:var(--card);border:1px solid var(--line);border-radius:20px;padding:20px;
    backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px);box-shadow:var(--shadow);margin-bottom:16px;
  }
  .panel h2{font-size:14px;margin-bottom:14px;color:var(--sub);font-weight:600;display:flex;align-items:center;gap:8px}
  .panel h2::before{content:"";width:4px;height:14px;border-radius:2px;background:var(--grad)}
  .acc-row{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
  .acc-row input{
    background:var(--card-strong);border:1.5px solid var(--line);border-radius:12px;color:var(--fg);
    padding:9px 13px;font-size:13.5px;min-width:180px;transition:border-color .15s, box-shadow .15s;
  }
  .acc-row input:focus{outline:none;border-color:var(--violet);box-shadow:0 0 0 4px rgba(139,92,246,.16)}
  .acc-row #acc-pwd{flex:1;min-width:220px}
  .acc-row #notify-h{flex:1;max-width:120px}

  table{width:100%;border-collapse:collapse;font-size:13.5px}
  th,td{text-align:left;padding:11px 8px}
  th{color:var(--sub);font-weight:600;font-size:12px;border-bottom:1px solid var(--line)}
  tbody tr{border-bottom:1px solid var(--line);transition:background .12s}
  tbody tr:last-child{border-bottom:0}
  tbody tr:hover{background:var(--grad-soft)}
  td.num{font-variant-numeric:tabular-nums;text-align:right;font-weight:600}
  .tag{display:inline-block;font-size:11px;border-radius:999px;padding:2px 9px;margin-left:7px}
  .tag.m{background:var(--okbg);color:var(--ok)}
  td .date{font-weight:600}
  .muted{color:var(--sub)}
  svg{width:100%;height:140px;display:block}
  .no{color:var(--sub);font-size:13px;padding:24px 0;text-align:center}
</style>
</head>
<body>
<div class="wrap">
<header>
  <div class="brand"><span class="dot"></span>加班时长</div>
  <span class="user" id="user"></span>
  <div class="op">
    <span class="updated" id="updated"></span>
    <button class="btn" onclick="refresh()">刷新</button>
  </div>
</header>

<div class="err" id="err"></div>

<div class="hero">
  <div class="label">本月总加班</div>
  <div class="big" id="total">-</div>
  <span class="range" id="range"></span>
  <span class="sub" id="crush"></span>
</div>

<div class="cards">
  <div class="card"><div class="top"><span class="ico" style="background:rgba(139,92,246,.16)">💼</span><span class="label">工作日加班</span></div><div class="value" id="workday">-</div><div class="foot">小时</div></div>
  <div class="card"><div class="top"><span class="ico" style="background:rgba(6,182,212,.16)">🏖️</span><span class="label">节假日加班</span></div><div class="value" id="holiday">-</div><div class="foot">小时</div></div>
  <div class="card"><div class="top"><span class="ico" style="background:rgba(236,72,153,.16)">📅</span><span class="label">加班天数</span></div><div class="value" id="times">-</div><div class="foot muted">天</div></div>
  <div class="card"><div class="top"><span class="ico" style="background:rgba(34,197,94,.16)">🍱</span><span class="label">餐补</span></div><div class="value" id="meal">-</div><div class="foot muted">元</div></div>
</div>

<div class="panel"><h2>近 30 天每日加班</h2><div id="chart"><div class="no">暂无数据</div></div></div>

<div class="panel">
  <h2>本月明细</h2>
  <div id="details"><table><thead><tr><th>日期</th><th>类型</th><th class="num">时长 (h)</th><th>打卡时段</th></tr></thead>
  <tbody id="rows"></tbody></table></div>
</div>

<div class="panel">
  <h2>账号设置</h2>
  <div class="acc-row">
    <input id="acc-user" placeholder="EHR 用户名" autocomplete="off">
    <input id="acc-pwd" type="password" placeholder="密码（留空则保持不变）" autocomplete="off">
    <button class="btn" onclick="saveAccount()">保存账号</button>
    <span id="acc-msg" class="updated"></span>
  </div>
</div>

<div class="panel">
  <h2>加班提醒</h2>
  <div class="acc-row">
    <span class="updated">每日累计加班达</span>
    <input id="notify-h" type="number" min="0" step="0.5" placeholder="2">
    <span class="updated">小时时通知（填 0 关闭）</span>
    <button class="btn" onclick="saveNotify()">保存</button>
    <span id="notify-msg" class="updated"></span>
  </div>
</div>
</div>
<script>
const MSG={ok:'✓ 已保存'};
function fill(data){
  if(data.error) document.getElementById('err').textContent='错误: '+data.error;
  else document.getElementById('err').textContent='';
  document.getElementById('user').textContent=data.user?('👤 '+data.user):'未设置账号';
  const accUser=document.getElementById('acc-user');
  if(document.activeElement!==accUser) accUser.value=data.user||'';
  const nh=document.getElementById('notify-h');
  if(document.activeElement!==nh) nh.value=data.notify_daily_hours||0;
  document.getElementById('updated').textContent=data.updated?('更新于 '+data.updated):'';
  document.getElementById('total').textContent=data.total+' h';
  document.getElementById('range').textContent=(data.begin?'📅 ':'')+(data.begin||'')+' ~ '+(data.end||'');
  const crush=data.workday&&data.holiday?('工作日 '+data.workday+'h · 节假日 '+data.holiday+'h'):'';
  document.getElementById('crush').textContent=crush;
  document.getElementById('workday').textContent=data.workday+' h';
  document.getElementById('holiday').textContent=data.holiday+' h';
  document.getElementById('times').textContent=data.times;
  document.getElementById('meal').textContent=data.meal;
  const rows=data.details||[];
  const tb=document.getElementById('rows'); tb.textContent='';
  for(const d of rows){
    const tr=document.createElement('tr');
    const tdDate=document.createElement('td'); tdDate.className='date'; tdDate.textContent=d[0];
    const tdType=document.createElement('td');
    const span=document.createElement('span');
    span.style.color=d[1].includes('节假日')?'#06b6d4':'#8b5cf6';
    span.style.fontWeight='600';
    span.textContent=d[1];
    tdType.appendChild(span);
    if(d[5]){const tag=document.createElement('span'); tag.className='tag m'; tag.textContent='🍱 餐'; tdType.appendChild(tag);}
    const tdHours=document.createElement('td'); tdHours.className='num'; tdHours.textContent=Number(d[2]).toFixed(2);
    const tdKq=document.createElement('td'); tdKq.className='muted'; tdKq.textContent=d[3]+' ~ '+d[4];
    tr.appendChild(tdDate); tr.appendChild(tdType); tr.appendChild(tdHours); tr.appendChild(tdKq);
    tb.appendChild(tr);
  }
  renderChart(data.history||[]);
}
function renderChart(his){
  const box=document.getElementById('chart');
  if(!his||his.length===0){box.innerHTML='<div class="no">暂无数据</div>';return;}
  const W=600,H=140,pad=8,max=Math.max(2,...his.map(p=>p[1]));
  const bw=Math.min(18,(W-pad*2)/his.length-3);
  let x=pad,out='<svg viewBox="0 0 '+W+' '+H+'" preserveAspectRatio="xMidYMid meet">';
  out+='<defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1">'+
    '<stop offset="0%" stop-color="#ec4899"/><stop offset="55%" stop-color="#8b5cf6"/>'+
    '<stop offset="100%" stop-color="#3b82f6"/></linearGradient></defs>';
  for(const p of his){
    const h=p[1],hh=Math.max(3,h/max*96),y=H-pad-hh;
    out+='<rect x="'+x+'" y="'+y+'" width="'+bw+'" height="'+hh+'" rx="5" fill="'+
      (h>8?'#ef4444':h>0?'url(#g)':'rgba(148,163,184,.30)')+'">'+
      '<title>'+p[0]+' 加班 '+h+'h</title></rect>';
    if(h>0) out+='<text x="'+(x+bw/2)+'" y="'+(y-5)+'" text-anchor="middle" font-size="10" fill="var(--sub)" font-variant-numeric="tabular-nums">'+h+'</text>';
    x+=bw+3;
  }
  out+='</svg>';
  box.innerHTML=out;
}
async function load(){
  try{
    const r=await fetch('/api/state');
    fill(await r.json());
  }catch(e){ document.getElementById('err').textContent='面板数据加载失败: '+e.message; }
}
function saveAccount(){
  const user=document.getElementById('acc-user').value.trim();
  const pwd=document.getElementById('acc-pwd').value;
  const msg=document.getElementById('acc-msg');
  msg.textContent='保存中...';
  fetch('/api/config',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({username:user,password:pwd})})
    .then(r=>r.json()).then(d=>{
      if(d.ok){
        msg.textContent=MSG.ok;
        document.getElementById('acc-pwd').value='';
        setTimeout(()=>msg.textContent='',3000);
        load();
      }else{
        msg.textContent='✗ '+(d.error||'保存失败');
      }
    }).catch(e=>msg.textContent='✗ '+e.message);
}
function saveNotify(){
  const v=parseFloat(document.getElementById('notify-h').value);
  if(isNaN(v)||v<0){document.getElementById('notify-msg').textContent='✗ 输入无效';return;}
  const msg=document.getElementById('notify-msg');
  fetch('/api/config',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({notify_daily_hours:v})})
    .then(r=>r.json()).then(d=>{
      if(d.ok){
        msg.textContent=MSG.ok;
        setTimeout(()=>msg.textContent='',3000);
        load();
      }else msg.textContent='✗ '+(d.error||'保存失败');
    }).catch(e=>msg.textContent='✗ '+e.message);
}
function refresh(){ fetch('/api/refresh',{method:'POST'}).then(load); }
load();
setInterval(load,5000);
</script>
</body>
</html>
""";
}