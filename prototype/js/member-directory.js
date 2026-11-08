(() => {
  'use strict';
  // Demo fixtures only. Production directory joins employees, assignments and member_profiles.
  const members=[
    {id:'1004',name:'د. خالد المحاضر',initials:'خم',college:'كلية الهندسة',department:'قسم هندسة الحاسوب',position:'عضو هيئة تدريس · عاقد دورة',expertise:['طرائق التدريس','تصميم الأنشطة','التعلم النشط'],bio:'يهتم بتطوير الممارسات التدريسية وتصميم الأنشطة التطبيقية. يشارك في برامج التطوير المهني كعضو هيئة تدريس، ويقدم دورات تدريبية عند تكليفه.',office:'مبنى كلية الهندسة'},
    {id:'1005',name:'م. رنا السلطاني',initials:'رس',college:'كلية الهندسة',department:'قسم هندسة الحاسوب',position:'عضو هيئة تدريس',expertise:['التعليم الرقمي','تصميم المناهج'],bio:'تهتم باستخدام الأدوات الرقمية في التعليم وتطوير المحتوى والأنشطة الصفية.',office:'لم يحدد بعد'},
    {id:'1003',name:'د. محمد العمري',initials:'مع',college:'كلية الهندسة',department:'عمادة الكلية',position:'عميد الكلية · عضو هيئة تدريس',expertise:['القيادة الأكاديمية','التطوير المهني'],bio:'يتابع برامج التطوير المهني واحتياجات الكلية التدريبية، ويشارك في الدورات كعضو هيئة تدريس.',office:'عمادة كلية الهندسة'},
    {id:'1002',name:'سارة الجودة',initials:'سج',college:'كلية العلوم',department:'قسم الرياضيات',position:'مسؤولة جودة · عضو هيئة تدريس',expertise:['ضمان الجودة','القياس والتقويم'],bio:'تهتم بقياس جودة البرامج التدريبية وتطوير أساليب تقييم مخرجات التعلم.',office:'لم يحدد بعد'},
    {id:'1006',name:'د. سمر الخالدي',initials:'سخ',college:'كلية الآداب',department:'قسم اللغة العربية',position:'عضو هيئة تدريس · محاضرة',expertise:['البحث العلمي','الكتابة العلمية'],bio:'تهتم بمهارات الكتابة العلمية وتطوير البحث، وتشارك خبرتها عبر البرامج التدريبية.',office:'لم يحدد بعد'},
    {id:'1007',name:'د. أحمد ناصر',initials:'أن',college:'كلية الهندسة',department:'قسم الهندسة الكهربائية',position:'عضو هيئة تدريس',expertise:['التعليم التطبيقي','القياس والتقويم'],bio:'يهتم بتطوير التجارب التطبيقية وربط المخرجات التعليمية بالممارسة.',office:'لم يحدد بعد'}
  ];
  const $=id=>document.getElementById(id),escape=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const normal=value=>value.toLowerCase().replace(/[ًٌٍَُِّْـ]/g,'').replace(/[أإآ]/g,'ا').replace(/ى/g,'ي');
  const tags=member=>member.expertise.map(tag=>`<span class="tag">${escape(tag)}</span>`).join('');
  [...new Set(members.map(m=>m.college))].forEach(college=>$('college-filter').add(new Option(college,college)));
  function departments(){const college=$('college-filter').value;$('department-filter').innerHTML='<option value="all">جميع الأقسام</option>';[...new Set(members.filter(m=>college==='all'||m.college===college).map(m=>m.department))].forEach(dept=>$('department-filter').add(new Option(dept,dept)));}
  function render(){
    const q=normal($('member-search').value.trim()),college=$('college-filter').value,dept=$('department-filter').value;
    const list=members.filter(m=>(college==='all'||m.college===college)&&(dept==='all'||m.department===dept)&&normal(`${m.name} ${m.expertise.join(' ')} ${m.position}`).includes(q));
    $('member-count').textContent=`${list.length} من ${members.length} أعضاء في الدليل التجريبي`;
    $('members').innerHTML=list.map(m=>`<a class="member-card" href="?member=${m.id}"><div class="avatar">${m.initials}</div><h3>${m.name}</h3><p>${m.position}</p><p>${m.college} · ${m.department}</p><div class="tags">${tags(m)}</div><span class="open-profile">عرض الملف الشخصي ←</span></a>`).join('')||'<div class="empty">لا يوجد أعضاء مطابقون. جرّب اسماً آخر أو غيّر الفلاتر.</div>';
  }
  departments();render();
  $('member-search').oninput=render;$('college-filter').onchange=()=>{departments();render();};$('department-filter').onchange=render;
  const id=new URLSearchParams(location.search).get('member');
  if(id){
    $('directory').hidden=true;$('profile').hidden=false;
    const m=members.find(m=>m.id===id);
    $('profile-content').innerHTML=m?`<div class="profile-header"><div class="avatar">${m.initials}</div><div><h2>${m.name}</h2><p>${m.position}</p><p>${m.college} · ${m.department}</p></div></div><div class="profile-layout"><article class="profile-section"><h3>نبذة مهنية</h3><p>${m.bio}</p><h3 style="margin-top:25px">مجالات الخبرة</h3><div class="tags">${tags(m)}</div></article><aside class="profile-section"><h3>البيانات المهنية</h3><dl><dt>الكلية</dt><dd>${m.college}</dd><dt>القسم</dt><dd>${m.department}</dd><dt>موقع المكتب</dt><dd>${m.office}</dd><dt>بريد التواصل المنشور</dt><dd>لم يضف العضو بريد تواصل</dd></dl></aside></div>`:'<div class="empty">الملف المطلوب غير موجود في الدليل التجريبي.</div>';
    if(m)document.title=`${m.name} — الملف الشخصي`;
  }
})();
