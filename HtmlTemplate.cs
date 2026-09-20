namespace EhrOvertimeTray;

/// <summary>Web 面板页面（单文件自含，明暗自适应，瑞士极简风：严格网格 + 黑白高对比 + 单一强调色 + 直角无装饰）</summary>
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
    --paper:#ffffff; --card:#ffffff; --row-hover:#f7f7f7;
    --ink:#111111; --sub:#666666;
    --line:#e4e4e4; --line-strong:#111111;
    --accent:#e10600; --bar:#111111; --bar-empty:#ececec;
  }
  @media (prefers-color-scheme:dark){
    :root{
      --paper:#0e0e0e; --card:#141414; --row-hover:#1b1b1b;
      --ink:#f2f2f2; --sub:#9a9a9a;
      --line:#2a2a2a; --line-strong:#f2f2f2;
      --accent:#ff3b30; --bar:#f2f2f2; --bar-empty:#242424;
    }
  }
  *{box-sizing:border-box;margin:0;padding:0}
  body{
    font-family:"Helvetica Neue",Inter,"Segoe UI Variable Text","Segoe UI",Arial,"PingFang SC","Microsoft YaHei",system-ui,sans-serif;
    color:var(--ink); background:var(--paper);
    min-height:100vh; padding:36px clamp(16px,4vw,48px) 56px;
    -webkit-font-smoothing:antialiased;
  }
  ::selection{background:var(--accent);color:#fff}
  .wrap{max-width:1040px;margin:0 auto}

  header{border-top:3px solid var(--line-strong);padding-top:18px;margin-bottom:30px;
    display:flex;align-items:center;gap:16px;flex-wrap:wrap}
  .brand{display:flex;align-items:center;gap:10px;font-size:19px;font-weight:700;letter-spacing:-.01em}
  .dot{width:11px;height:11px;background:var(--accent)}
  .user{border:1px solid var(--line);padding:6px 12px;font-size:12px;color:var(--sub);font-variant-numeric:tabular-nums}
  .op{margin-left:auto;display:flex;gap:14px;align-items:center;flex-wrap:wrap}
  .updated{color:var(--sub);font-size:11px;letter-spacing:.06em;font-variant-numeric:tabular-nums}
  .btn{
    background:var(--ink);color:var(--paper);border:0;border-radius:0;padding:10px 20px;
    font-size:13px;font-weight:600;letter-spacing:.04em;cursor:pointer;
    transition:background-color .2s ease;
  }
  .btn:hover{background:var(--accent);color:#fff}
  .btn:active{opacity:.85}
  .btn:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
  .err{color:var(--accent);font-size:13px;font-weight:500;margin-bottom:16px}
  .err:empty{display:none}

  .hero{border-bottom:2px solid var(--line-strong);padding:6px 0 28px;margin-bottom:32px}
  .hero .label{font-size:12px;font-weight:600;letter-spacing:.14em;color:var(--sub)}
  .hero .big{font-size:clamp(56px,9vw,88px);font-weight:700;letter-spacing:-.03em;line-height:1.05;
    font-variant-numeric:tabular-nums;margin:4px 0 12px}
  .hero .meta{display:flex;gap:20px;flex-wrap:wrap;color:var(--sub);font-size:13px;font-variant-numeric:tabular-nums}

  .cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));
    gap:1px;background:var(--line);border:1px solid var(--line);margin-bottom:32px}
  .card{background:var(--card);padding:18px 20px 16px;animation:fade .35s ease backwards}
  .card:nth-child(2){animation-delay:.05s}.card:nth-child(3){animation-delay:.10s}.card:nth-child(4){animation-delay:.15s}
  @keyframes fade{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}
  .card .top{display:flex;align-items:baseline;gap:8px;margin-bottom:12px}
  .card .no{font-size:11px;font-weight:700;color:var(--accent);font-variant-numeric:tabular-nums;letter-spacing:.05em}
  .card .label{color:var(--sub);font-size:12.5px}
  .card .value{font-size:30px;font-weight:700;letter-spacing:-.01em;font-variant-numeric:tabular-nums;line-height:1.15}
  .card .foot{color:var(--sub);font-size:11px;margin-top:3px;letter-spacing:.04em}

  .panel{background:var(--card);border:1px solid var(--line);padding:22px;margin-bottom:32px;animation:fade .35s ease backwards}
  .panel h2{font-size:13px;font-weight:700;letter-spacing:.12em;display:flex;align-items:center;gap:14px;margin-bottom:16px}
  .panel h2::after{content:"";flex:1;height:1px;background:var(--line)}

  .acc-row{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
  .acc-row input{
    background:var(--card);border:1px solid var(--line-strong);border-radius:0;color:var(--ink);
    padding:10px 12px;font-size:13.5px;min-width:180px;transition:border-color .2s ease;
  }
  .acc-row input:focus{outline:none;border-color:var(--accent)}
  .acc-row #acc-pwd{flex:1;min-width:220px}
  .acc-row #notify-h{flex:1;max-width:120px}

  table{width:100%;border-collapse:collapse;font-size:13.5px}
  th,td{text-align:left;padding:11px 8px}
  th{color:var(--sub);font-weight:600;font-size:11px;letter-spacing:.08em;border-bottom:1px solid var(--line-strong)}
  tbody tr{border-bottom:1px solid var(--line);transition:background .15s ease}
  tbody tr:last-child{border-bottom:0}
  tbody tr:hover{background:var(--row-hover)}
  td.num{font-variant-numeric:tabular-nums;text-align:right;font-weight:600}
  .tag{display:inline-block;font-size:11px;border-radius:0;padding:1px 8px;margin-left:7px;
    border:1px solid var(--accent);color:var(--accent);font-weight:600;letter-spacing:.05em;vertical-align:1px}
  td .date{font-weight:600;font-variant-numeric:tabular-nums}
  .muted{color:var(--sub);font-variant-numeric:tabular-nums}
  svg{width:100%;height:140px;display:block}
  .no{color:var(--sub);font-size:13px;padding:24px 0;text-align:center}

  @media (prefers-reduced-motion:reduce){
    *,*::before,*::after{animation:none!important;transition:none!important}
  }
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
  <div class="meta"><span id="range"></span><span id="crush"></span></div>
</div>

<div class="cards">
  <div class="card"><div class="top"><span class="no">01</span><span class="label">工作日加班</span></div><div class="value" id="workday">-</div><div class="foot">小时</div></div>
  <div class="card"><div class="top"><span class="no">02</span><span class="label">节假日加班</span></div><div class="value" id="holiday">-</div><div class="foot">小时</div></div>
  <div class="card"><div class="top"><span class="no">03</span><span class="label">加班天数</span></div><div class="value" id="times">-</div><div class="foot muted">天</div></div>
  <div class="card"><div class="top"><span class="no">04</span><span class="label">餐补</span></div><div class="value" id="meal">-</div><div class="foot muted">元</div></div>
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
  document.getElementById('user').textContent=data.user||'未设置账号';
  const accUser=document.getElementById('acc-user');
  if(document.activeElement!==accUser) accUser.value=data.user||'';
  const nh=document.getElementById('notify-h');
  if(document.activeElement!==nh) nh.value=data.notify_daily_hours||0;
  document.getElementById('updated').textContent=data.updated?('更新于 '+data.updated):'';
  document.getElementById('total').textContent=data.total+' h';
  document.getElementById('range').textContent=(data.begin||'')+(data.begin?' ~ ':'')+(data.end||'');
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
    span.style.color=d[1].includes('节假日')?'var(--accent)':'var(--ink)';
    span.style.fontWeight='600';
    span.textContent=d[1];
    tdType.appendChild(span);
    if(d[5]){const tag=document.createElement('span'); tag.className='tag m'; tag.textContent='餐补'; tdType.appendChild(tag);}
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
  for(const p of his){
    const h=p[1],hh=Math.max(3,h/max*96),y=H-pad-hh;
    out+='<rect x="'+x+'" y="'+y+'" width="'+bw+'" height="'+hh+'" fill="'+
      (h>8?'var(--accent)':h>0?'var(--bar)':'var(--bar-empty)')+'">'+
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
