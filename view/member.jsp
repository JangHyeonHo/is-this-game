<%@ page language="java" contentType="text/html; charset=UTF-8"
	pageEncoding="UTF-8"%>
<!DOCTYPE html>
<html>
<head>
<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
<title>Sign Up</title>
<script src="https://code.jquery.com/jquery-3.4.1.min.js"
	integrity="sha256-CSXorXvZcTkaix6Yvo6HppcZGetbYMGWSFlBw8HfCJo="
	crossorigin="anonymous"></script>
<link
	href="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css"
	rel="stylesheet"
	integrity="sha384-ggOyR0iXCbMQv3Xipma34MD+dH/1fQ784/j6cY/iJTQUOhcWr7x9JvoRxT2MZw1T"
	crossorigin="anonymous">
<script
	src="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/js/bootstrap.min.js"
	integrity="sha384-JjSmVgyd0p3pXB1rRibZUAYoIIy6OrQ6VrjIEaFf/nJGzIxFDsf4x0xIM+B07jRM"
	crossorigin="anonymous"></script>
<script
	src="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/js/bootstrap.bundle.min.js"
	integrity="sha384-xrRywqdh3PHs8keKZN+8zzc5TX0GRTLCcmivcbNJWm2rs5C8PRhcEn3czEjhAO9o"
	crossorigin="anonymous"></script>



<!-- <script>
// Example starter JavaScript for disabling form submissions if there are invalid fields
(function() {
  'use strict';
  window.addEventListener('load', function() {
    // Fetch all the forms we want to apply custom Bootstrap validation styles to
    var forms = document.getElementsByClassName('needs-validation');
    // Loop over them and prevent submission
    var validation = Array.prototype.filter.call(forms, function(form) {
      form.addEventListener('submit', function(event) {
        if (form.checkValidity() === false) {
          event.preventDefault();
          event.stopPropagation();
        }
        form.classList.add('was-validated');
      }, false);
    });
  }, false);
})();
</script> -->

<script>
$(function(){
	//에러 체크 용 validate. 0 : 아직 체크하지 않음 | 1 : 문제 없음 | 2 : 에러 발생
	var vaild = ["was-validated","is-valid","is-invalid"];
	var vaildFeedBack = ["valid-feedback","invalid-feedback"];
	var idErr = false;
	var pwErr = false;
	var pwCErr = false;
	//이미지 파일을 등록했을 때 프리뷰를 변경함.
	//-- change preview start --
	$("#picFile").on("change",function(e){
		var file = e.target.files[0],
        reader = new FileReader(),
        $picSample = $("#picSample");
		$picLabel = $("#picLabel");

        //파일을 등록하지 않았을 경우에는 이미지를 삭제한다.
        if(file==null || file=="undefined"){
        	$picSample.empty();
        	$picLabel.text("이미지를 선택해 주세요");
        	return false;
        }
    	// 그림 파일이 아니면 아무것도 하지 않는다.
    	if(file.type.indexOf("image") < 0){
      		return false;
    	}

    	// 파일 불러오기가 완료했을 때 이벤트 등록한다ファイル読み込みが完了した際のイベント登録
    	reader.onload = (function(file) {
      		return function(e) {
        //기존의 프리뷰를 삭제.
        	$picSample.empty();
        // .prevew의 영역 안에 불러온 그림을 표시하는 image 태그를 추가
        	$picSample.append($('<img>').attr({
                  src: e.target.result,
                  style: "width : 100%; height : 100%;",
                  class: "preview",
                  title: file.name
              }));
      		};
    	})(file);
    	reader.readAsDataURL(file);
    	//label의 파일명을 변경한다.
    	$picLabel.text(file.name);
	})
	//-- change preview end --

	//아이디의 Email형식을 확인.
	//-- email validate check start --
	$("#account").on("keyup focusout",function(){
		var ere = /[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?/g;
		$account = $("#account");
		$idFeedBack = $("#idErr");
		$thisVal = $account.val();
		var RegExpCheck = ere.test($thisVal);
		if($thisVal.length == 0){
			validCheck($account,0,vaild);
			feedBackClass($idFeedBack,false)
			$idFeedBack.text("")
			idErr = false;
		} else if($thisVal.length > 0 && RegExpCheck){
			if(!$account.hasClass(vaild[1])){
				validCheck($account,1,vaild);
				feedBackClass($idFeedBack,true)
				$idFeedBack.text("正しいEmail形式です。")
			}
			idErr = true;
		} else if($thisVal.length > 0 && !RegExpCheck){
			if(!$account.hasClass(vaild[2])){
				validCheck($account,2,vaild);
				feedBackClass($idFeedBack,false)
				$idFeedBack.text("Email形式を合わせてください。");
			}
			idErr = false;
		}
		/*
		if($thisVal.length == 0){
			if(!$account.hasClass(vaild[0])){
				$account.addClass(vaild[0])
			}
			if($account.hasClass(vaild[1])){
				$account.removeClass(vaild[1])
			}
			if($account.hasClass(vaild[2])){
				$account.removeClass(vaild[2])
			}
		}
		if($thisVal.length > 0 && !RegExpCheck){
			if($account.hasClass(vaild[0])){
				$account.removeClass(vaild[0])
			}
			if($account.hasClass(vaild[1])){
				$account.removeClass(vaild[1])
			}
			if(!$account.hasClass(vaild[2])){
				$account.addClass(vaild[2])
			}
		} else if($thisVal.length > 0 && RegExpCheck){
			if($account.hasClass(vaild[0])){
				$account.removeClass(vaild[0])
			}
			if(!$account.hasClass(vaild[1])){
				$account.addClass(vaild[1])
			}
			if($account.hasClass(vaild[2])){
				$account.removeClass(vaild[2])
			}
		}
		*/
	})
	//-- email validate check end --

	//Password에 포함하고 있는 문자 확인.
	//-- password validate check start --
	$("#pw").on("keyup focusout",function(){
		var pre = /^(?=.*\d)(?=.*[#$-/:-?{-~!"^_`\[\]])(?=.*[a-zA-Z]).{8,}$/gm
		$password = $("#pw");
		$pwFeedBack = $("#pwErr");
		$thisVal = $password.val();

		if($("#pwConfirm").val().length > 0){
			$("#pwConfirm").val("")
			feedBackClass($("#pwConErr"),false)
			validCheck($("#pwConfirm"),0,vaild);
			pwCErr = false;
		}

		var RegExpCheck = pre.test($thisVal);
		if($thisVal.length == 0){
			feedBackClass($pwFeedBack,false)
			validCheck($password,0,vaild);
			pwErr = false;
		} else if($thisVal.length <= 8){
			if(!$password.hasClass(vaild[2])){
				validCheck($password,2,vaild);
				feedBackClass($pwFeedBack,false)
			}
			$pwFeedBack.text("비밀번호는 8자 이상의 문자로 입력해 주세요.");
			pwErr = false;
		} else if($thisVal.length > 8 && !RegExpCheck){
			if(!$password.hasClass(vaild[2])){
				validCheck($password,2,vaild);
				feedBackClass($pwFeedBack,false)
			}
			$pwFeedBack.text("비밀번호에 특수문자, 숫자, 문자를 반드시 포함해 주세요.");
			pwErr = false;
		} else if($thisVal.length > 8 && RegExpCheck){
			if(!$password.hasClass(vaild[1])){
				validCheck($password,1,vaild);
				feedBackClass($pwFeedBack,true)
				$pwFeedBack.text("올바른 비밀번호 입니다.")
			}
			pwErr = true;
		}
	})
	//-- password validate check end --

	//비밀번호 확인 체크.
	//-- password confirm validate check start --
	$("#pwConfirm").on("keyup focusout",function(){
			$password = $("#pw");
			$pwConfirm = $("#pwConfirm");
			$pwFeedBack = $("#pwConErr");
			$pasVal = $password.val();
			$conVal = $pwConfirm.val();
			if($conVal.length == 0){
				feedBackClass($pwFeedBack,false)
				validCheck($pwConfirm,0,vaild);
				pwCErr = false;
			} else if($pasVal.length > 0 && pwErr && $pasVal == $conVal){ //비밀번호를 입력하고, 에러가 없는 상태에서 비교했을 때 일치했다면?
				validCheck($pwConfirm,1,vaild);
				feedBackClass($pwFeedBack,true)
				$pwFeedBack.text("비밀번호가 일치합니다.");
				pwCErr = true;
			} else if($pasVal.length > 0 && pwErr && $pasVal != $conVal){ //비밀번호를 입력하고, 에러가 없는 상태에서 비교했을 때 일치하지 않았다면?
				if(!$pwConfirm.hasClass(vaild[2])){
					validCheck($pwConfirm,2,vaild);
					feedBackClass($pwFeedBack,false)
				}
				$pwFeedBack.text("비밀번호가 일치하지 않습니다.");
				pwCErr = false;
			} else if(!pwErr){ //비밀번호 에러가 있는 상태.
				if(!$pwConfirm.hasClass(vaild[2])){
					validCheck($pwConfirm,2,vaild);
					feedBackClass($pwFeedBack,false)
				}
				$pwFeedBack.text("먼저 올바른 비밀번호를 입력해 주세요.");
				pwCErr = false;
			}
	})
	//-- password confirm validate check end --

	//등록 체크
	//-- submit check start--
	$("#submitBtn").on("click",function(){
		if(!idErr || !pwErr || !pwCErr){
			alert("폼을 확인해 주세요.")
			return false;
		}
		alert("회원 가입을 환영합니다!")
	})
	//--submit check end--

	//에러 체크 코드의 간략화.
	//-- validation check start--
	function validCheck($val, addIndex, arr){
		for(var i = 0; i < arr.length; i++){
			if($val.hasClass(arr[i])){
				$val.removeClass(arr[i]);
			}
			if(i == addIndex){
				$val.addClass(arr[i]);
			}
		}
	}
	//피드백 체크 코드의 간략화.
	function feedBackClass($val, valid){
		if(valid){
			$val.removeClass(vaildFeedBack[1]);
			$val.addClass(vaildFeedBack[0]);
		} else{
			$val.removeClass(vaildFeedBack[0]);
			$val.addClass(vaildFeedBack[1]);
		}
	}
		//-- validation check end--

})
</script>

<style type="text/css">
header,footer {
	width: 100%;
	height: 150px;
	float: left;
}

#contents {
	min-width: 1280px;
	width: 100%;
	float: left;
}

#picSample {
	width: 200px;
	height: 250px;
}
</style>
</head>
<body>
	<header></header>
	<div id="contents">
	<h2 class = "mb-4">회원가입</h2>
		<form action="./member" method="post" class = "w-50" id = "frm">
				<div class="form-group form-check w-75 mx-auto">
					<textarea cols="70" rows="10" class ="w-100" style="resize: none;">회원 규약</textarea>
					<br>
					<div>
						<input type="checkbox" name="signup" class="form-check-input"
							required><span class="form-check-label">해당 규약을 전부 읽었고, 개인정보를 취급하는 것에 대해 동의합니다.</span>
					</div>
				</div>
				<div id="picSample" class="border border-secondary rounded mb-4 mx-auto"></div>
				<div class="custom-file mb-4">
				<input type="file" value="사진 등록" id ="picFile" name="mPic"
					class="form-control-sm custom-file-input" accept="image/jpeg,image/gif,image/png" "/>
					<label class="custom-file-label text-secondary" for="picFile" id = "picLabel">이미지를 선택해 주세요.</label>
				</div>
				<div class = "form-group mb-4">
					<input type="text" id = "account" name="id" placeholder="아이디는 Email형식으로 입력해 주세요."
					maxlength="128" class="form-control was-validated" required />
					<div id="idErr"	class="invalid-feedback ml-2"></div>
				</div>
				<div class = "form-group mb-4">
				<input type="password" id = "pw" name="pw"
					placeholder="비밀번호는 특수문자, 숫자를 포함해 8자 이상으로 해 주세요" maxlength="16"
					class="form-control was-validated" required />
					<div id="pwErr" class="invalid-feedback ml-2"></div>
				</div>
				<div class = "form-group mb-4">
				<input type="password" name="pwConfirm" id ="pwConfirm"
					placeholder="한 번 더 비밀번호를 입력해 주세요." maxlength="16"
					class="form-control was-validated" required />
					<div id="pwConErr" class="invalid-feedback ml-2"></div>
				</div>
				<div class="form-group form-check form-check-inline">
					<input type="radio" class="form-check-input" name="sex" value = "M" required
						checked /><span class="form-check-label">남 </span>
				</div>
				<div class="form-group form-check form-check-inline">
					<input type="radio" class="form-check-input" name="sex" value = "W" /><span
						class="form-check-label">녀</span>
				</div>
				<div class="row mb-3">
					<div class="col">
						<input type="text" name="lastName" class="form-control"
							placeholder="성"  required/>
					</div>
					<div class="col">
						<input type="text" name="firstName" class="form-control"
							placeholder="이름"  required/>
					</div>
				</div>
			<input type="submit" id = "submitBtn"value="가입" class="btn btn-primary">
		</form>
	</div>
	<footer></footer>
</body>
</html>